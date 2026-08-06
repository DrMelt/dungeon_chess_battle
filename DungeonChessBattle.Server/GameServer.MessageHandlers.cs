using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Server.Stores;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// GameServer 的大厅 JSON 消息分发与各 Handle* 处理、广播与发送工具。
/// 状态查询与写入统一经 <see cref="IGameStateStore"/>，不再直接触碰具体数据结构。
/// </summary>
public partial class GameServer {
    /// <summary>
    /// 处理大厅收到的自定义 JSON 消息，按消息类型分发。
    /// </summary>
    private void OnCustomPacket(NetPeer peer, ReadOnlySpan<byte> data) {
        try {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.GetProperty(MessageProperty.Type).GetString();

            switch (type) {
                case MessageType.CreateRoom:
                    HandleCreateRoom(peer, root);
                    break;
                case MessageType.JoinRoom:
                    HandleJoinRoom(peer, root);
                    break;
                case MessageType.ListRooms:
                    HandleListRooms(peer);
                    break;
                case MessageType.PrepareAddUnit:
                    HandlePrepareAddUnit(peer, root);
                    break;
                case MessageType.PrepareRemoveUnit:
                    HandlePrepareRemoveUnit(peer, root);
                    break;
                case MessageType.PrepareStartBattle:
                    HandlePrepareStartBattle(peer, root);
                    break;
                case MessageType.PrepareReady:
                    HandlePrepareReady(peer, root);
                    break;
                case MessageType.PrepareUnready:
                    HandlePrepareUnready(peer, root);
                    break;
                case MessageType.ReconnectRoom:
                    HandleReconnectRoom(peer, root);
                    break;
                default:
                    _logger.LogWarning("[Game] Unknown command: {Type}", type);
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[Game] Custom packet error");
        }
    }

    /// <summary>
    /// 校验服务器密码；不匹配时发送失败响应。
    /// </summary>
    private bool ValidateServerPassword(NetPeer peer, JsonElement root, string responseType, string? roomId) {
        string? serverPassword = root.TryGetProperty(MessageProperty.ServerPassword, out var sp) ? sp.GetString() : null;
        if (!string.IsNullOrEmpty(_config.ServerPassword) && serverPassword != _config.ServerPassword) {
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "Invalid server password."));
            return false;
        }
        return true;
    }

    private bool TryGetRoomParams(NetPeer peer, JsonElement root, string responseType,
        out string roomId, out string playerId) {
        roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() ?? "" : "";
        playerId = root.TryGetProperty(MessageProperty.PlayerId, out var ip) ? ip.GetString() ?? "" : "";

        if (string.IsNullOrWhiteSpace(roomId)) {
            _logger.LogWarning("[Game] {Type}: roomId is required.", responseType);
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "roomId is required."));
            return false;
        }
        return true;
    }

    private static string GetDisplayName(JsonElement root, string playerId) {
        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var np) ? np.GetString() : null;
        if (playerName == null)
            return $"Player_{playerId[..Math.Min(playerId.Length, 6)]}";
        // 超长拒绝（安全优于截断，避免两个玩家显示名碰撞）
        return playerName.Length <= EntityConstants.MaxPlayerNameLength
            ? playerName
            : $"Player_{playerId[..Math.Min(playerId.Length, 6)]}";
    }

    /// <summary>
    /// 处理 create_room：注册房间（准备阶段不重定向）。
    /// </summary>
    private void HandleCreateRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.CreateRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.CreateRoomResponse, out string roomId, out string playerId))
            return;

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;

        // 解析招募板配置
        GameRoom? config;
        if (root.TryGetProperty(MessageProperty.Config, out var configEl)) {
            config = new GameRoom(roomId) {
                Title = configEl.TryGetProperty(MessageProperty.Title, out var t) ? t.GetString() ?? "" : "",
                DungeonName = configEl.TryGetProperty(MessageProperty.DungeonName, out var dn) ? dn.GetString() ?? "" : "",
                Description = configEl.TryGetProperty(MessageProperty.Description, out var d) ? d.GetString() ?? "" : "",
                Category = configEl.TryGetProperty(MessageProperty.Category, out var c) && c.TryGetByte(out var cb)
                    ? (RoomCategory)cb : RoomCategory.Casual,
                HostName = configEl.TryGetProperty(MessageProperty.HostName, out var hn) ? hn.GetString() ?? "" : "",
                MaxPlayers = configEl.TryGetProperty(MessageProperty.MaxPlayers, out var mp) && mp.TryGetInt32(out var mpv)
                    ? mpv : 2,
                CurrentPlayers = 1,
            };
        }
        else {
            // 无配置时使用默认值，房间标题用 roomId
            config = new GameRoom(roomId) {
                Title = roomId,
                HostName = GetDisplayName(root, playerId),
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
        }

        // 房主 displayName 由服务端权威解析（不信任客户端 config 中的 HostName）
        string hostDisplayName = GetDisplayName(root, playerId);

        // 组合原子注册：单锁内完成房间注册 + 房主登记 + 成员登记 + peer 归属 + playerId
        if (!_stateStore.TryRegisterRoomWithHost(roomId, actualRoomPassword, config,
                hostDisplayName, playerId, peer.Id)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Failed to register room."));
            return;
        }

        // 注册创建者到房间 peer 列表
        _lobby.RegisterPeerToRoom(roomId, peer);

        // 准备阶段：不重定向，只返回成功
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, true));

        // 向房主广播最新准备状态与单位列表（房主占位卡初始化，与 join_room 广播保持对称）
        BroadcastPrepareRoomState(roomId);
        BroadcastPrepareUnitList(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created (prepare), player='{Player}' ({PlayerId}), Title={Title}.",
                roomId, hostDisplayName, playerId, config.Title);
    }

    /// <summary>
    /// 处理 join_room：验证房间与密码（准备阶段不重定向）。
    /// </summary>
    private void HandleJoinRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.JoinRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.JoinRoomResponse, out string roomId, out string playerId))
            return;

        if (!_stateStore.RoomExists(roomId)) {
            _logger.LogWarning("[Game] join_room: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Room not found."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_stateStore.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] join_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Invalid room password."));
            return;
        }

        // 原子自增玩家数（避免并发 join 时读改写丢失更新）
        _stateStore.IncrementPlayerCount(roomId);
        _lobby.RegisterPeerToRoom(roomId, peer);

        string displayName = GetDisplayName(root, playerId);
        // 登记玩家为房间准备成员（默认未准备，playerId 一并登记用于战斗白名单）
        _stateStore.RegisterRoomPlayer(roomId, displayName, playerId, peer.Id);

        // 准备阶段加入：不重定向，直接成功
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, true));

        // 向房间内所有玩家广播最新准备状态（包含新加入玩家的默认未准备状态）
        BroadcastPrepareRoomState(roomId);

        // 向房间内所有玩家广播最新单位列表（新加入玩家立即获得已有职业选择）
        BroadcastPrepareUnitList(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' ({PlayerId}) joined room '{RoomId}' (prepare).",
                displayName, playerId, roomId);
    }

    /// <summary>
    /// 处理 list_rooms：返回招募板房间列表。
    /// </summary>
    private void HandleListRooms(NetPeer peer) {
        try {
            var rooms = _stateStore.ListActiveRooms();
            SendToPeer(peer, MessageWriter.WriteListRoomsResponse(rooms));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Game] Sent listing of {Count} rooms.", rooms.Count);
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[Game] Error generating room listing.");
        }
    }

    /// <summary>
    /// 处理 prepare_add_unit：为房间添加准备单位，并广播最新列表。
    /// </summary>
    private void HandlePrepareAddUnit(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? unitName = root.TryGetProperty(MessageProperty.UnitName, out var un) ? un.GetString() : null;
        string camp = root.TryGetProperty(MessageProperty.Camp, out var cp) ? cp.GetString() ?? CampConstants.CampA : CampConstants.CampA;

        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(unitName)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, false, "roomId and unitName required."));
            return;
        }

        if (unitName.Length > EntityConstants.MaxUnitNameLength || !CampConstants.IsValidCamp(camp)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, false, "Invalid unit params."));
            return;
        }

        // 单位归属用服务端权威玩家名（peer 归属表），不信任客户端提交
        string? ownerName = _stateStore.GetPlayerNameForPeer(peer.Id);
        if (ownerName == null) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, false, "Player not in room."));
            return;
        }

        if (!_stateStore.AddPrepareUnit(roomId, unitName, camp, ownerName)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, false, "Room not found."));
            return;
        }

        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareAddUnit, roomId, true));

        // 广播更新给房间内所有玩家
        BroadcastPrepareUnitList(roomId);
    }

    /// <summary>
    /// 处理 prepare_remove_unit：从房间移除准备单位，成功时广播最新列表。
    /// </summary>
    private void HandlePrepareRemoveUnit(NetPeer peer, JsonElement root) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? unitName = root.TryGetProperty(MessageProperty.UnitName, out var un) ? un.GetString() : null;
        string camp = root.TryGetProperty(MessageProperty.Camp, out var cp) ? cp.GetString() ?? CampConstants.CampA : CampConstants.CampA;

        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(unitName)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareRemoveUnit, roomId, false, "roomId and unitName required."));
            return;
        }

        bool removed = _stateStore.RemovePrepareUnit(roomId, unitName, camp);
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareRemoveUnit, roomId, removed,
            removed ? null : "Unit not found."));

        if (removed)
            BroadcastPrepareUnitList(roomId);
    }

    /// <summary>
    /// 处理 prepare_start_battle：仅房主可发起，且需除房主外所有玩家已准备。
    /// 校验通过后创建战斗房间服务器并返回重定向端口。
    /// </summary>
    private void HandlePrepareStartBattle(NetPeer peer, JsonElement root) {
        if (!TryGetRoomParams(peer, root, MessageType.PrepareStartBattleResponse, out string roomId, out string playerId))
            return;

        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var pn) ? pn.GetString() : null;
        if (string.IsNullOrWhiteSpace(playerName)) {
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }
        if (playerName.Length > EntityConstants.MaxPlayerNameLength) {
            _logger.LogWarning("[Game] start_battle: player name too long for '{PlayerId}'.", playerId);
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }

        if (!_stateStore.RoomExists(roomId)) {
            _logger.LogWarning("[Game] start_battle: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }

        // 校验发起者必须是房主（基于 peer 归属表，不信任客户端提交的 playerName）
        if (!_stateStore.IsPeerRoomHost(peer.Id, roomId)) {
            _logger.LogWarning("[Game] start_battle: peer {PeerId} is not the host of room '{RoomId}', rejected.", peer.Id, roomId);
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }

        // 校验除房主外所有玩家已准备
        if (!_stateStore.IsAllOthersReady(roomId)) {
            _logger.LogWarning("[Game] start_battle: room '{RoomId}' has not-ready players, rejected.", roomId);
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }

        // 创建 BattleRoomServer：初始化（根实体与单位迁移）由房间线程从 Store 自取完成，
        // 连接资格由 Store 成员表实时查询——此处不再向服务器预注册白名单
        var server = _lobby.StartRoomBattle(roomId);

        // 向房间内所有玩家广播重定向（含端口号），确保非房主也能进入战斗
        var redirectMsg = MessageWriter.WritePrepareStartBattleResponse(roomId, server.Port);
        foreach (var p in _lobby.GetRoomPeers(roomId))
            SendToPeer(p, redirectMsg);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' battle started on port {Port}.",
                roomId, server.Port);
    }

    /// <summary>
    /// 处理 prepare_ready：非房主请求准备，更新状态并广播房间准备状态。
    /// </summary>
    private void HandlePrepareReady(NetPeer peer, JsonElement root) {
        HandlePrepareReadyState(peer, root, true);
    }

    /// <summary>
    /// 处理 prepare_unready：非房主请求取消准备，更新状态并广播房间准备状态。
    /// </summary>
    private void HandlePrepareUnready(NetPeer peer, JsonElement root) {
        HandlePrepareReadyState(peer, root, false);
    }

    /// <summary>准备/取消准备的公共处理：校验房间与成员身份后更新状态并广播。</summary>
    private void HandlePrepareReadyState(NetPeer peer, JsonElement root, bool ready) {
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var pn) ? pn.GetString() : null;
        string responseType = ready ? MessageType.PrepareReady : MessageType.PrepareUnready;

        if (string.IsNullOrEmpty(roomId) || string.IsNullOrEmpty(playerName)) {
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "roomId and playerName required."));
            return;
        }

        if (!_stateStore.RoomExists(roomId)) {
            SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, false, "Room not found."));
            return;
        }

        _stateStore.SetPlayerReady(roomId, playerName, ready);
        SendToPeer(peer, MessageWriter.WriteResponse(responseType, roomId, true));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' {Action} in room '{RoomId}'.",
                playerName, ready ? "ready" : "unready", roomId);

        // 广播最新准备状态给房间内所有玩家
        BroadcastPrepareRoomState(roomId);
    }

    /// <summary>
    /// 处理 reconnect_room：校验身份后将断线玩家重连到战斗房间。
    /// </summary>
    private void HandleReconnectRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.ReconnectRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.ReconnectRoomResponse, out string roomId, out string playerId))
            return;

        string? playerName = root.TryGetProperty(MessageProperty.PlayerName, out var pn) ? pn.GetString() : null;
        if (string.IsNullOrWhiteSpace(playerName)) {
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Player name is required."));
            return;
        }
        if (playerName.Length > EntityConstants.MaxPlayerNameLength) {
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Player name too long."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_stateStore.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] reconnect_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Invalid room password."));
            return;
        }

        var server = _lobby.GetRoomServer(roomId);
        if (server == null) {
            _logger.LogWarning("[Game] reconnect_room: room '{RoomId}' not in battle.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Room not in battle."));
            return;
        }

        server.UpdatePlayerName(playerId, playerName);
        server.RegisterPlayer(playerId, playerName);
        SendToPeer(peer, MessageWriter.WriteJoinRoomRedirect(roomId, server.Port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{PlayerName}' ({PlayerId}) reconnected to room '{RoomId}' on port {Port}.",
                playerName, playerId, roomId, server.Port);
    }

    /// <summary>
    /// 将房间当前准备单位列表广播给该房间所有 peer。
    /// </summary>
    private void BroadcastPrepareUnitList(string roomId) {
        var units = _stateStore.GetPrepareUnits(roomId);
        var peers = _lobby.GetRoomPeers(roomId);
        var msg = MessageWriter.WritePrepareUnitList(roomId,
            units.Select(u => (u.UnitName, u.Camp, u.PlayerName)));

        foreach (var p in peers)
            SendToPeer(p, msg);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Game] Broadcast prepare units to {PeerCount} peers in room '{RoomId}' ({UnitCount} units)",
                peers.Count, roomId, units.Count);
    }

    /// <summary>
    /// 将房间当前准备状态广播给该房间所有 peer。
    /// </summary>
    private void BroadcastPrepareRoomState(string roomId) {
        var state = _stateStore.GetRoomState(roomId);
        var peers = _lobby.GetRoomPeers(roomId);
        var msg = MessageWriter.WritePrepareRoomState(roomId, state.HostName, state.DungeonName,
            state.Players.Select(p => (p.PlayerName, p.Ready)));

        foreach (var p in peers)
            SendToPeer(p, msg);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Game] Broadcast prepare room state to {PeerCount} peers in room '{RoomId}' ({PlayerCount} players)",
                peers.Count, roomId, state.Players.Count);
    }

    /// <summary>
    /// 可靠有序地发送消息给指定 peer。
    /// </summary>
    private static void SendToPeer(NetPeer peer, byte[] messageBytes) {
        peer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }
}
