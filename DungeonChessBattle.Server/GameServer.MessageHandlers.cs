using System.Text;
using System.Text.Json;
using LiteNetLib;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Entities;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// GameServer 的大厅 JSON 消息分发与各 Handle* 处理、广播与发送工具。
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
        if (!string.IsNullOrEmpty(_serverPassword) && serverPassword != _serverPassword) {
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

        if (_lobby.RoomExists(roomId)) {
            _logger.LogWarning("[Game] Room '{RoomId}' already exists.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Room already exists."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;

        // 解析招募板配置
        GameRoom? config;
        if (root.TryGetProperty(MessageProperty.Config, out var configEl)) {
            config = new GameRoom(roomId) {
                Title = configEl.TryGetProperty(MessageProperty.Title, out var t) ? t.GetString() ?? "" : "",
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

        if (!_lobby.RegisterRoom(roomId, actualRoomPassword, config)) {
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, false, "Failed to register room."));
            return;
        }

        // 注册创建者到房间 peer 列表
        _lobby.RegisterPeerToRoom(roomId, peer);

        // 准备阶段：不重定向，只返回成功
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.CreateRoomResponse, roomId, true));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created (prepare), player='{Player}' ({PlayerId}), Title={Title}.",
                roomId, config.HostName, playerId, config.Title);
    }

    /// <summary>
    /// 处理 join_room：验证房间与密码（准备阶段不重定向）。
    /// </summary>
    private void HandleJoinRoom(NetPeer peer, JsonElement root) {
        if (!ValidateServerPassword(peer, root, MessageType.JoinRoomResponse, null)
            || !TryGetRoomParams(peer, root, MessageType.JoinRoomResponse, out string roomId, out string playerId))
            return;

        if (!_lobby.RoomExists(roomId)) {
            _logger.LogWarning("[Game] join_room: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Room not found."));
            return;
        }

        string? roomPassword = root.TryGetProperty(MessageProperty.Password, out var pp) ? pp.GetString() : null;
        string? actualRoomPassword = string.IsNullOrEmpty(roomPassword) ? null : roomPassword;
        if (!_lobby.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] join_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, false, "Invalid room password."));
            return;
        }

        _lobby.UpdatePlayerCount(roomId, _lobby.GetRoomConfig(roomId)?.CurrentPlayers + 1 ?? 1);
        _lobby.RegisterPeerToRoom(roomId, peer);

        // 准备阶段加入：不重定向，直接成功
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.JoinRoomResponse, roomId, true));

        string displayName = GetDisplayName(root, playerId);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' ({PlayerId}) joined room '{RoomId}' (prepare).",
                displayName, playerId, roomId);
    }

    /// <summary>
    /// 处理 list_rooms：返回招募板房间列表。
    /// </summary>
    private void HandleListRooms(NetPeer peer) {
        try {
            var rooms = _lobby.GetRoomListings();
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

        if (!_lobby.AddPrepareUnit(roomId, unitName, camp)) {
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

        bool removed = _lobby.RemovePrepareUnit(roomId, unitName, camp);
        SendToPeer(peer, MessageWriter.WriteResponse(MessageType.PrepareRemoveUnit, roomId, removed,
            removed ? null : "Unit not found."));

        if (removed)
            BroadcastPrepareUnitList(roomId);
    }

    /// <summary>
    /// 处理 prepare_start_battle：创建战斗房间服务器并返回重定向端口。
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

        if (!_lobby.RoomExists(roomId)) {
            _logger.LogWarning("[Game] start_battle: room '{RoomId}' not found.", roomId);
            SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, 0));
            return;
        }

        // 创建 BattleRoomServer 并迁移单位
        var server = _lobby.StartRoomBattle(roomId);
        server.RegisterPlayer(playerId, playerName);

        // 发送重定向（含端口号）
        SendToPeer(peer, MessageWriter.WritePrepareStartBattleResponse(roomId, server.Port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' battle started (port {Port}), player='{Player}' ({PlayerId}) redirected.",
                roomId, server.Port, playerName, playerId);
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
        if (!_lobby.ValidateRoomPassword(roomId, actualRoomPassword)) {
            _logger.LogWarning("[Game] reconnect_room: invalid password for room '{RoomId}'.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Invalid room password."));
            return;
        }

        var server = _lobby.GetRoomServer(roomId);
        if (server == null) {
            _logger.LogWarning("[Game] reconnect_room: room '{RoomId}' not in battle.", roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Room not in battle or expired."));
            return;
        }

        if (!server.CanReconnect(playerId)) {
            _logger.LogWarning("[Game] reconnect_room: player '{PlayerId}' not eligible for reconnect in room '{RoomId}'.",
                playerId, roomId);
            SendToPeer(peer, MessageWriter.WriteReconnectRoomResponse(roomId, false, "Reconnect timeout or player not in room."));
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
        var units = _lobby.GetPrepareUnits(roomId);
        var peers = _lobby.GetRoomPeers(roomId);
        var msg = MessageWriter.WritePrepareUnitList(roomId, units);

        foreach (var p in peers)
            SendToPeer(p, msg);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Game] Broadcast prepare units to {PeerCount} peers in room '{RoomId}' ({UnitCount} units)",
                peers.Count, roomId, units.Count);
    }

    /// <summary>
    /// 可靠有序地发送消息给指定 peer。
    /// </summary>
    private static void SendToPeer(NetPeer peer, byte[] messageBytes) {
        peer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }
}
