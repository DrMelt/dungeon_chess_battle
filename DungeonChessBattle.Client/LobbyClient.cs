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

    // ── 房间操作事件 ──
    public event Action<string>? OnRoomJoined;
    public event Action<string>? OnRoomCreated;

    /// <summary>大厅重定向到房间端口 (roomId, port)</summary>
    public event Action<string, int>? OnRedirectToRoom;

    /// <summary>重连失败事件</summary>
    public event Action<string>? OnReconnectFailed;

    /// <summary>招募板房间列表接收事件</summary>
    public event Action<List<RoomListing>>? OnRoomListReceived;

    /// <summary>准备阶段战斗启动重定向事件 (roomId, port)</summary>
    public event Action<string, int>? OnPrepareBattleRedirect;

    /// <summary>准备阶段单位列表更新事件</summary>
    public event Action<string, List<(string UnitName, byte Camp)>>? OnPrepareUnitListUpdated;

    // ── 请求方法 ──

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

    public void RequestJoinRoom(string roomId) {
        SendCommand(MessageWriter.WriteRoomRequestFull(
            MessageType.JoinRoom, roomId, "", null, "", null));
    }

    /// <summary>
    /// 请求添加准备阶段单位。
    /// </summary>
    public void RequestPrepareAddUnit(string roomId, string unitName, byte camp) {
        SendCommand(MessageWriter.WritePrepareAddUnit(roomId, unitName, camp));
    }

    /// <summary>
    /// 请求移除准备阶段单位。
    /// </summary>
    public void RequestPrepareRemoveUnit(string roomId, string unitName) {
        SendCommand(MessageWriter.WritePrepareRemoveUnit(roomId, unitName));
    }

    /// <summary>
    /// 请求开始战斗。
    /// </summary>
    public void RequestPrepareStartBattle(string roomId) {
        SendCommand(MessageWriter.WritePrepareStartBattle(roomId));
    }

    // ── OnNetworkReceive ──

    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        HandleCustomPacket(data);
    }

    // ── JSON 协议处理 ──

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

    private void HandleListRoomsResponse(JsonElement root) {
        var rooms = new List<RoomListing>();
        if (root.TryGetProperty(MessageProperty.Rooms, out var arr) && arr.ValueKind == JsonValueKind.Array) {
            foreach (var item in arr.EnumerateArray()) {
                rooms.Add(new RoomListing {
                    RoomId = item.TryGetProperty(MessageProperty.RoomId, out var rid) ? rid.GetString() ?? "" : "",
                    Title = item.TryGetProperty(MessageProperty.Title, out var t) ? t.GetString() ?? "" : "",
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

    private void HandlePrepareStartBattleResponse(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        int port = root.TryGetProperty(MessageProperty.Port, out var pp) && pp.TryGetInt32(out var p) ? p : 0;

        if (!string.IsNullOrEmpty(roomId) && port > 0) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[LobbyClient] Prepare battle redirect to room '{RoomId}' on port {Port}", roomId, port);
            _pendingEventInvocations.Enqueue(() => OnPrepareBattleRedirect?.Invoke(roomId, port));
        }
    }

    private void HandlePrepareUnitResponse(JsonElement root, bool isAdd) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;

        if (success && _logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[LobbyClient] Prepare {Action} unit in {RoomId}: succeeded", isAdd ? "add" : "remove", roomId);
    }

    private void HandlePrepareUnitList(JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        var units = new List<(string UnitName, byte Camp)>();

        if (root.TryGetProperty("units", out var arr) && arr.ValueKind == JsonValueKind.Array) {
            foreach (var item in arr.EnumerateArray()) {
                string name = item.TryGetProperty(MessageProperty.UnitName, out var un) ? un.GetString() ?? "" : "";
                byte camp = item.TryGetProperty(MessageProperty.Camp, out var cp) && cp.TryGetByte(out var cb) ? cb : (byte)1;
                units.Add((name, camp));
            }
        }

        if (roomId != null)
            _pendingEventInvocations.Enqueue(() => OnPrepareUnitListUpdated?.Invoke(roomId, units));
    }
}
