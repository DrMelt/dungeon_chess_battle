using DungeonChessBattle.Lobby.Shared;
using DungeonChessBattle.Lobby.Protocol;
using DungeonChessBattle.Lobby.Protocol.Dtos;
using DungeonChessBattle.Server.Abstractions;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// GameServer 的战斗编排请求处理。
/// 大厅业务，创建、加入、列房与准备等，由 Server.Lobby 的
/// <see cref="GameLobby"/> 承担；
/// 本文件仅保留涉及战斗房间生命周期的协调编排：开始战斗、断线重连。
/// 战斗房间生命周期服务经 <see cref="IBattleRoomManager"/> 契约调用，不感知具体实现。
/// </summary>
public partial class GameServer {
    /// <summary>
    /// 处理 prepare_start_battle：仅房主可发起，且需除房主外所有玩家已准备。
    /// 房间与发起者身份均从连接归属反查，不信任客户端提交。
    /// 校验通过后创建战斗房间服务器，经 <see cref="IBattleRoomManager"/>，并向房间内所有玩家广播重定向端口。
    /// </summary>
    public async Task<LobbyResult> HandleStartBattleAsync(string connectionId) {
        string? roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? playerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId == null || string.IsNullOrEmpty(playerName))
            return new LobbyResult(string.Empty, false, "Player not in room.");

        if (!_stateStore.RoomExists(roomId)) {
            _logger.LogWarning("start_battle: room '{RoomId}' not found.", roomId);
            return new LobbyResult(roomId, false, "Room not found.");
        }

        // 校验发起者必须是房主，基于连接归属表，不信任客户端提交
        if (!_stateStore.IsConnectionRoomHost(connectionId, roomId)) {
            _logger.LogWarning("start_battle: connection of room '{RoomId}' is not the host, rejected.", roomId);
            return new LobbyResult(roomId, false, "Only room host can start battle.");
        }

        // 校验除房主外所有玩家已准备
        if (!_stateStore.IsAllOthersReady(roomId)) {
            _logger.LogWarning("start_battle: room '{RoomId}' has not-ready players, rejected.", roomId);
            return new LobbyResult(roomId, false, "Not all players ready.");
        }

        // 校验所有玩家（含房主）都已选择角色
        if (!_stateStore.AreAllPlayersUnitSelected(roomId)) {
            _logger.LogWarning("start_battle: room '{RoomId}' has players without unit selection, rejected.", roomId);
            return new LobbyResult(roomId, false, "Not all players selected a unit.");
        }

        // 创建 BattleRoomServer：初始化，根实体与单位迁移，由房间线程从 Store 自取完成
        int port = _battleRoomManager.StartRoomBattle(roomId);

        // 房间状态迁移由拥有状态所有权的协调层执行，战斗实现层不触碰房间状态
        _stateStore.UpdateRoomStatus(roomId, RoomStatus.InProgress);

        // 向房间内所有玩家广播重定向，含端口号，确保非房主也能进入战斗
        await BroadcastToRoomAsync(roomId, HubMethods.OnPrepareBattleRedirect,
            new RoomRedirect(roomId, port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room '{RoomId}' battle started on port {Port}.", roomId, port);

        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 处理 reconnect_room：身份从登录会话反查，重连仅恢复既有会话。
    /// </summary>
    public async Task<LobbyResult> HandleReconnectRoomAsync(string connectionId, ReconnectRoomRequest req) {
        if (string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(string.Empty, false, "roomId required.");

        // 玩家名从登录会话取服务端权威身份，不信任客户端自报
        string? loginName = _stateStore.GetLoginPlayerName(connectionId);
        if (string.IsNullOrEmpty(loginName))
            return new LobbyResult(req.RoomId, false, "Player not logged in.");

        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;
        if (!_stateStore.ValidateRoomPassword(req.RoomId, actualRoomPassword))
            return new LobbyResult(req.RoomId, false, "Invalid room password.");

        if (!_battleRoomManager.TryGetRoomPort(req.RoomId, out int port))
            return new LobbyResult(req.RoomId, false, "Room not in battle.");

        // 登记房间成员，供战斗白名单校验；仅房间已有同名会话才允许，杜绝冒用他人 playerId 绑单位
        if (!_battleRoomManager.RegisterPlayer(req.RoomId, req.PlayerId, loginName))
            return new LobbyResult(req.RoomId, false, "Reconnect rejected: session mismatch.");

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player '{PlayerName}' ({PlayerId}) reconnected to room '{RoomId}' on port {Port}.",
                loginName, req.PlayerId, req.RoomId, port);

        return new LobbyResult(req.RoomId, true, Port: port);
    }
}
