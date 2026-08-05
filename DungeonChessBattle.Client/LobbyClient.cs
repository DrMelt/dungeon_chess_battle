using System.Text;
using System.Text.Json;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 大厅客户端，负责与大厅端口 (10170) 的 JSON 协议通信。
/// 处理 create_room、join_room、list_rooms、reconnect_room 请求及对应的响应/重定向。
/// 不包含 LES Entity 系统。
/// </summary>
public class LobbyClient(ILogger<LobbyClient> logger) : NetworkClientBase(logger) {

    /// <summary>成功加入房间事件。参数：房间 ID。</summary>
    public event Action<string>? OnRoomJoined;

    /// <summary>成功创建房间事件。参数：房间 ID。</summary>
    public event Action<string>? OnRoomCreated;

    /// <summary>大厅重定向到房间端口事件。参数：房间 ID、端口。</summary>
    public event Action<string, int>? OnRedirectToRoom;

    /// <summary>重连失败事件。参数：错误信息。</summary>
    public event Action<string>? OnReconnectFailed;

    /// <summary>招募板房间列表接收事件。</summary>
    public event Action<List<RoomListing>>? OnRoomListReceived;

    /// <summary>准备阶段战斗启动重定向事件。参数：房间 ID、端口。</summary>
    public event Action<string, int>? OnPrepareBattleRedirect;

    /// <summary>准备阶段单位列表更新事件。参数：房间 ID、单位列表（含归属玩家名）。</summary>
    public event Action<string, List<(string UnitName, string Camp, string PlayerName)>>? OnPrepareUnitListUpdated;

    /// <summary>准备阶段房间准备状态更新事件。参数：房间 ID、房主名、副本名、玩家(名, 准备标志)列表。</summary>
    public event Action<string, string, string, List<(string PlayerName, bool Ready)>>? OnPrepareRoomStateUpdated;

    /// <summary>最近一次单位列表广播缓存（按房间 ID）。网络线程写入，用于解决"订阅晚于广播"的初始状态丢失。</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,
        List<(string UnitName, string Camp, string PlayerName)>> _recentUnitLists = new();

    /// <summary>最近一次房间状态广播缓存（按房间 ID）。网络线程写入，用于解决"订阅晚于广播"的初始状态丢失。</summary>
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string,
        (string HostName, string DungeonName, List<(string PlayerName, bool Ready)> Players)> _recentRoomStates = new();

    /// <summary>
    /// 请求创建房间（含招募板配置）。
    /// </summary>
    public void RequestCreateRoom(string roomId, string playerName, string playerId,
        string? roomPassword, GameRoom? config, string? serverPassword = null) {
        var effectiveConfig = config ?? new GameRoom(roomId);
        SendCommand(MessageWriter.WriteCreateRoomRequest(roomId, playerName, playerId,
            roomPassword, effectiveConfig, serverPassword));
    }

    /// <summary>
    /// 请求房间列表（招募板）。
    /// </summary>
    public void RequestListRooms() {
        SendCommand(MessageWriter.WriteRoomRequest(MessageType.ListRooms, ""));
    }

    /// <summary>由 GameClientService 内部调用，重定向后桥接 OnRoomJoined 事件。</summary>
    internal void TriggerRoomJoined(string roomId) {
        _pendingEventInvocations.Enqueue(() => OnRoomJoined?.Invoke(roomId));
    }

    /// <summary>请求加入房间。</summary>
    public void RequestJoinRoom(string roomId) {
        SendCommand(MessageWriter.WriteRoomRequestFull(
            MessageType.JoinRoom, roomId, "", null, "", null));
    }

    /// <summary>
    /// 请求添加准备阶段单位。
    /// </summary>
    public void RequestPrepareAddUnit(string roomId, string unitName, string camp) {
        SendCommand(MessageWriter.WritePrepareAddUnit(roomId, unitName, camp));
    }

    /// <summary>
    /// 请求移除准备阶段单位。
    /// </summary>
    public void RequestPrepareRemoveUnit(string roomId, string unitName) {
        SendCommand(MessageWriter.WritePrepareRemoveUnit(roomId, unitName));
    }

    /// <summary>
    /// 请求开始战斗（仅房主可发起，需其他玩家已全部准备）。
    /// </summary>
    public void RequestPrepareStartBattle(string roomId, string playerName, string playerId) {
        SendCommand(MessageWriter.WritePrepareStartBattle(roomId, playerName, playerId));
    }

    /// <summary>
    /// 请求准备（非房主）。
    /// </summary>
    public void RequestPrepareReady(string roomId, string playerName) {
        SendCommand(MessageWriter.WritePrepareReady(roomId, playerName));
    }

    /// <summary>
    /// 请求取消准备（非房主）。
    /// </summary>
    public void RequestPrepareUnready(string roomId, string playerName) {
        SendCommand(MessageWriter.WritePrepareUnready(roomId, playerName));
    }

    /// <summary>收到网络数据时处理 JSON 包。</summary>
    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        HandleCustomPacket(data);
    }

    /// <summary>
    /// 解析并分发大厅 JSON 协议包。
    /// </summary>
    private void HandleCustomPacket(ReadOnlySpan<byte> data) {
        try {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.TryGetProperty(MessageProperty.Type, out var tp) ? tp.GetString() : null;

            switch (type) {
                case MessageType.JoinRoomResponse:
                    HandleJoinRoomResponse(root);
                    break;
                case MessageType.JoinRoomRedirect:
                    HandleRedirectToRoom(root);
                    break;
                case MessageType.CreateRoomResponse:
                    HandleCreateRoomResponse(root);
                    break;
                case MessageType.ListRoomsResponse:
                    HandleListRoomsResponse(root);
                    break;
                case MessageType.PrepareStartBattleResponse:
                    HandlePrepareStartBattleResponse(root);
                    break;
                case MessageType.PrepareAddUnit:
                    HandlePrepareUnitResponse(root, true);
                    break;
                case MessageType.PrepareRemoveUnit:
                    HandlePrepareUnitResponse(root, false);
                    break;
                case MessageType.PrepareUnitList:
                    HandlePrepareUnitList(root);
                    break;
                case MessageType.PrepareRoomState:
                    HandlePrepareRoomState(root);
                    break;
                case MessageType.PrepareReady:
                case MessageType.PrepareUnready:
                    HandlePrepareReadyResponse(root);
                    break;
                case MessageType.ReconnectRoomResponse:
                    HandleReconnectRoomResponse(root);
                    break;
                default:
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("[LobbyClient] Unknown custom packet: {Type}", type);
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "[LobbyClient] Custom packet parse error");
        }
    }

    /// <summary>处理加入房间响应。</summary>
    private void HandleJoinRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (success && !string.IsNullOrEmpty(roomId)) {
            _pendingEventInvocations.Enqueue(() => OnRoomJoined?.Invoke(roomId));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Join room succeeded: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Join room failed: {Error}", error ?? "unknown");
        }
    }

    /// <summary>处理加入房间重定向（切到房间端口）。</summary>
    private void HandleRedirectToRoom(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        int port = root.TryGetProperty(MessageProperty.Port, out var pp) && pp.TryGetInt32(out var p) ? p : 0;

        if (!string.IsNullOrEmpty(roomId) && port > 0) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Redirecting to room '{RoomId}' on port {Port}", roomId, port);
            _pendingEventInvocations.Enqueue(() => OnRedirectToRoom?.Invoke(roomId, port));
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Redirect failed: invalid roomId or port");
        }
    }

    /// <summary>处理创建房间响应。</summary>
    private void HandleCreateRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (success && !string.IsNullOrEmpty(roomId)) {
            _pendingEventInvocations.Enqueue(() => OnRoomCreated?.Invoke(roomId));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Create room succeeded: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Create room failed: {Error}", error ?? "unknown");
        }
    }

    /// <summary>处理房间列表响应（招募板）。</summary>
    private void HandleListRoomsResponse(JsonElement root) {
        var rooms = new List<RoomListing>();
        if (root.TryGetProperty(MessageProperty.Rooms, out var arr) && arr.ValueKind == JsonValueKind.Array) {
            foreach (var item in arr.EnumerateArray()) {
                rooms.Add(new RoomListing {
                    RoomId = item.TryGetProperty(MessageProperty.RoomId, out var rid) ? rid.GetString() ?? "" : "",
                    Title = item.TryGetProperty(MessageProperty.Title, out var t) ? t.GetString() ?? "" : "",
                    DungeonName = item.TryGetProperty(MessageProperty.DungeonName, out var dn) ? dn.GetString() ?? "" : "",
                    Category = item.TryGetProperty(MessageProperty.Category, out var c) && c.TryGetByte(out var cb)
                        ? (RoomCategory)cb : RoomCategory.Casual,
                    HostName = item.TryGetProperty(MessageProperty.HostName, out var hn) ? hn.GetString() ?? "" : "",
                    CurrentPlayers = item.TryGetProperty(MessageProperty.CurrentPlayers, out var cp) && cp.TryGetInt32(out var cpv)
                        ? cpv : 0,
                    MaxPlayers = item.TryGetProperty(MessageProperty.MaxPlayers, out var mp) && mp.TryGetInt32(out var mpv)
                        ? mpv : 2,
                    HasPassword = item.TryGetProperty(MessageProperty.HasPassword, out var hp) && hp.GetBoolean(),
                    Status = item.TryGetProperty(MessageProperty.Status, out var s) && s.TryGetByte(out var sb)
                        ? (RoomStatus)sb : RoomStatus.Waiting,
                    CreatedAt = item.TryGetProperty(MessageProperty.CreatedAt, out var ca) && ca.TryGetDateTime(out var dt)
                        ? dt : DateTime.MinValue,
                });
            }
        }
        _pendingEventInvocations.Enqueue(() => OnRoomListReceived?.Invoke(rooms));
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[LobbyClient] Received listing of {Count} rooms.", rooms.Count);
    }

    /// <summary>处理重连响应；失败时触发 OnReconnectFailed，成功走重定向流程。</summary>
    private void HandleReconnectRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (!success) {
            string errorMsg = error ?? "Reconnect failed";
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[LobbyClient] Reconnect failed: {Error}", errorMsg);
            _pendingEventInvocations.Enqueue(() => OnReconnectFailed?.Invoke(errorMsg));
        }
        // 成功时会走已有的 HandleRedirectToRoom 流程（服务端返回重定向）
    }

    /// <summary>处理准备阶段战斗启动重定向。</summary>
    private void HandlePrepareStartBattleResponse(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        int port = root.TryGetProperty(MessageProperty.Port, out var pp) && pp.TryGetInt32(out var p) ? p : 0;

        if (!string.IsNullOrEmpty(roomId) && port > 0) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Prepare battle redirect to room '{RoomId}' on port {Port}", roomId, port);
            _pendingEventInvocations.Enqueue(() => OnPrepareBattleRedirect?.Invoke(roomId, port));
        }
    }

    /// <summary>处理准备阶段单位添加/移除响应。</summary>
    private void HandlePrepareUnitResponse(JsonElement root, bool isAdd) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;

        if (success && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[LobbyClient] Prepare {Action} unit in {RoomId}: succeeded", isAdd ? "add" : "remove", roomId);
    }

    /// <summary>处理准备阶段单位列表广播。</summary>
    private void HandlePrepareUnitList(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        var units = new List<(string UnitName, string Camp, string PlayerName)>();

        if (root.TryGetProperty(MessageProperty.Units, out var arr) && arr.ValueKind == JsonValueKind.Array) {
            foreach (var item in arr.EnumerateArray()) {
                string name = item.TryGetProperty(MessageProperty.UnitName, out var un) ? un.GetString() ?? "" : "";
                string camp = item.TryGetProperty(MessageProperty.Camp, out var cp) ? cp.GetString() ?? "" : "";
                string playerName = item.TryGetProperty(MessageProperty.PlayerName, out var pn) ? pn.GetString() ?? "" : "";
                units.Add((name, camp, playerName));
            }
        }

        if (roomId != null) {
            // 缓存最近列表：EnterRoom 时重放，覆盖订阅晚于广播导致的初始丢失
            _recentUnitLists[roomId] = units;
            _pendingEventInvocations.Enqueue(() => OnPrepareUnitListUpdated?.Invoke(roomId, units));
        }
    }

    /// <summary>处理准备/取消准备的响应确认。</summary>
    private void HandlePrepareReadyResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;

        if (success && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[LobbyClient] Prepare ready-state change in {RoomId}: succeeded", roomId);
    }

    /// <summary>处理准备阶段房间准备状态广播。</summary>
    private void HandlePrepareRoomState(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string hostName = root.TryGetProperty(MessageProperty.HostName, out var hp) ? hp.GetString() ?? "" : "";
        string dungeonName = root.TryGetProperty(MessageProperty.DungeonName, out var dn) ? dn.GetString() ?? "" : "";
        var players = new List<(string PlayerName, bool Ready)>();

        if (root.TryGetProperty(MessageProperty.Players, out var arr) && arr.ValueKind == JsonValueKind.Array) {
            foreach (var item in arr.EnumerateArray()) {
                string name = item.TryGetProperty(MessageProperty.PlayerName, out var pn) ? pn.GetString() ?? "" : "";
                bool ready = item.TryGetProperty(MessageProperty.Ready, out var rp2) && rp2.GetBoolean();
                players.Add((name, ready));
            }
        }

        if (roomId != null) {
            // 缓存最近状态：EnterRoom 时重放，覆盖订阅晚于广播导致的初始丢失
            _recentRoomStates[roomId] = (hostName, dungeonName, players);
            _pendingEventInvocations.Enqueue(() => OnPrepareRoomStateUpdated?.Invoke(roomId, hostName, dungeonName, players));
        }
    }

    /// <summary>获取指定房间最近一次单位列表广播缓存（网络线程写入，EnterRoom 重放用）。</summary>
    public bool TryGetRecentUnitList(string roomId,
        out List<(string UnitName, string Camp, string PlayerName)> units) {
        return _recentUnitLists.TryGetValue(roomId, out units!);
    }

    /// <summary>获取指定房间最近一次房间状态广播缓存（网络线程写入，EnterRoom 重放用）。</summary>
    public bool TryGetRecentRoomState(string roomId, out string hostName, out string dungeonName,
        out List<(string PlayerName, bool Ready)> players) {
        if (_recentRoomStates.TryGetValue(roomId, out var state)) {
            hostName = state.HostName;
            dungeonName = state.DungeonName;
            players = state.Players;
            return true;
        }
        hostName = "";
        dungeonName = "";
        players = [];
        return false;
    }

    /// <summary>断开连接时清理房间状态缓存，避免陈旧数据残留。</summary>
    protected override void OnDisconnectCleanup() {
        base.OnDisconnectCleanup();
        _recentUnitLists.Clear();
        _recentRoomStates.Clear();
    }

    /// <summary>重连时清理房间状态缓存，避免陈旧数据残留。</summary>
    protected override void OnReconnectCleanup() {
        base.OnReconnectCleanup();
        _recentUnitLists.Clear();
        _recentRoomStates.Clear();
    }
}
