using DungeonChessBattle.Protocol;
using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Server.Abstractions;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

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

        // 向房间内所有玩家广播重定向，含端口号，确保非房主也能进入战斗
        await BroadcastToRoomAsync(roomId, HubMethods.OnPrepareBattleRedirect,
            new RoomRedirect(roomId, port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room '{RoomId}' battle started on port {Port}.", roomId, port);

        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 处理 reconnect_room：校验身份后将断线玩家重连到战斗房间。
    /// </summary>
    public async Task<LobbyResult> HandleReconnectRoomAsync(ReconnectRoomRequest req) {
        if (string.IsNullOrWhiteSpace(req.RoomId) || string.IsNullOrWhiteSpace(req.PlayerName))
            return new LobbyResult(req.RoomId, false, "roomId and playerName required.");

        if (req.PlayerName.Length > EntityConstants.MaxPlayerNameLength)
            return new LobbyResult(req.RoomId, false, "Player name too long.");

        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;
        if (!_stateStore.ValidateRoomPassword(req.RoomId, actualRoomPassword))
            return new LobbyResult(req.RoomId, false, "Invalid room password.");

        if (!_battleRoomManager.TryGetRoomPort(req.RoomId, out int port))
            return new LobbyResult(req.RoomId, false, "Room not in battle.");

        _battleRoomManager.RegisterPlayer(req.RoomId, req.PlayerId, req.PlayerName);
        _battleRoomManager.UpdatePlayerName(req.RoomId, req.PlayerId, req.PlayerName);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player '{PlayerName}' ({PlayerId}) reconnected to room '{RoomId}' on port {Port}.",
                req.PlayerName, req.PlayerId, req.RoomId, port);

        return new LobbyResult(req.RoomId, true, Port: port);
    }
}
