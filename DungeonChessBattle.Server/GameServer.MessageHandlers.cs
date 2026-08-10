using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Core.Network.Dtos;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Server.Domain.Lobby;
using DungeonChessBattle.Server.Domain.Stores;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// GameServer 的各请求业务处理与房间广播实现。
/// 状态查询与写入统一经 <see cref="IGameStateStore"/>；向客户端返回 <see cref="LobbyResult"/>，
/// 房间内广播经 <see cref="ILobbyBroadcaster"/> 端口注入实现（不依赖具体传输）。
/// </summary>
public partial class GameServer {
    /// <summary>
    /// 校验服务器密码；不匹配时返回 false（调用方负责构造失败结果）。
    /// </summary>
    private bool ValidateServerPassword(string? serverPassword, string responseDesc, string? roomId) {
        if (!string.IsNullOrEmpty(_config.ServerPassword) && serverPassword != _config.ServerPassword) {
            _logger.LogWarning("[Game] {Desc}: invalid server password (room '{RoomId}').", responseDesc, roomId);
            return false;
        }
        return true;
    }

    /// <summary>解析权威玩家显示名：空或超长时退化为 Player_{playerId 前 6 位}。</summary>
    private static string GetDisplayName(string? playerName, string playerId) {
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
    public async Task<LobbyResult> HandleCreateRoomAsync(string connectionId, CreateRoomRequest req) {
        if (!ValidateServerPassword(req.ServerPassword, "CreateRoom", null)
            || string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId is required.");

        string roomId = req.RoomId;
        string playerId = req.PlayerId;
        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;

        // 房主 displayName 由服务端权威解析（不信任客户端提交的 HostName）
        string hostDisplayName = GetDisplayName(req.PlayerName, playerId);

        GameRoom config;
        if (req.Config != null) {
            config = new GameRoom(roomId) {
                Title = req.Config.Title,
                DungeonName = req.Config.DungeonName,
                Description = req.Config.Description,
                Category = req.Config.Category,
                HostName = hostDisplayName,
                MaxPlayers = req.Config.MaxPlayers > 0 ? req.Config.MaxPlayers : 2,
                CurrentPlayers = 1,
            };
        }
        else {
            // 无配置时使用默认值，房间标题用 roomId
            config = new GameRoom(roomId) {
                Title = roomId,
                HostName = hostDisplayName,
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
        }

        // 组合原子注册：单锁内完成房间注册 + 房主登记 + 成员登记 + 连接归属 + playerId
        if (!_stateStore.TryRegisterRoomWithHost(roomId, actualRoomPassword, config,
                hostDisplayName, playerId, connectionId))
            return new LobbyResult(roomId, false, "Failed to register room.");

        // 加入房间连接分组（准备阶段广播用）
        await _broadcaster.AddToRoomAsync(connectionId, roomId);

        await BroadcastRoomSnapshotAsync(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' created (prepare), player='{Player}' ({PlayerId}), Title={Title}.",
                roomId, hostDisplayName, playerId, config.Title);

        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 处理 join_room：验证房间与密码（准备阶段不重定向）。
    /// </summary>
    public async Task<LobbyResult> HandleJoinRoomAsync(string connectionId, JoinRoomRequest req) {
        if (!ValidateServerPassword(req.ServerPassword, "JoinRoom", null)
            || string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId is required.");

        if (!_stateStore.RoomExists(req.RoomId))
            return new LobbyResult(req.RoomId, false, "Room not found.");

        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;
        if (!_stateStore.ValidateRoomPassword(req.RoomId, actualRoomPassword))
            return new LobbyResult(req.RoomId, false, "Invalid room password.");

        // 原子自增玩家数（避免并发 join 时读改写丢失更新）
        _stateStore.IncrementPlayerCount(req.RoomId);
        await _broadcaster.AddToRoomAsync(connectionId, req.RoomId);

        string displayName = GetDisplayName(req.PlayerName, req.PlayerId);
        // 登记玩家为房间准备成员（默认未准备，playerId 一并登记用于战斗白名单）
        _stateStore.RegisterRoomPlayer(req.RoomId, displayName, req.PlayerId, connectionId);

        await BroadcastRoomSnapshotAsync(req.RoomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' ({PlayerId}) joined room '{RoomId}' (prepare).",
                displayName, req.PlayerId, req.RoomId);

        return new LobbyResult(req.RoomId, true);
    }

    /// <summary>
    /// 处理 list_rooms：返回招募板房间列表。
    /// </summary>
    public Task<RoomListResult> HandleListRoomsAsync() {
        var rooms = _stateStore.ListActiveRooms();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Sent listing of {Count} rooms.", rooms.Count);
        return Task.FromResult(new RoomListResult([.. rooms]));
    }
    /// <summary>
    /// 处理 prepare_add_unit：为房间添加准备单位，并广播最新列表。
    /// </summary>
    public async Task<LobbyResult> HandleAddPrepareUnitAsync(string connectionId, PrepareAddUnitRequest req) {
        if (string.IsNullOrEmpty(req.RoomId) || string.IsNullOrEmpty(req.UnitName))
            return new LobbyResult(req.RoomId, false, "roomId and unitName required.");

        if (req.UnitName.Length > EntityConstants.MaxUnitNameLength || !CampConstants.IsValidCamp(req.Camp))
            return new LobbyResult(req.RoomId, false, "Invalid unit params.");

        // 单位归属用服务端权威玩家名（连接归属表），不信任客户端提交
        string? ownerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (ownerName == null)
            return new LobbyResult(req.RoomId, false, "Player not in room.");

        // 反查该玩家的持久 playerId（控制器绑定用权威键，与连接密钥一致）
        string? ownerPlayerId = _stateStore.GetRoomPlayerIds(req.RoomId).GetValueOrDefault(ownerName);
        if (string.IsNullOrEmpty(ownerPlayerId))
            return new LobbyResult(req.RoomId, false, "Player identity not registered.");

        if (!_stateStore.AddPrepareUnit(req.RoomId, req.UnitName, req.Camp, ownerName, ownerPlayerId))
            return new LobbyResult(req.RoomId, false, "Room not found.");

        // 广播更新给房间内所有玩家
        await BroadcastRoomSnapshotAsync(req.RoomId);
        return new LobbyResult(req.RoomId, true);
    }

    /// <summary>
    /// 处理 prepare_remove_unit：从房间移除准备单位，成功时广播最新列表。
    /// 仅允许单位归属者（连接权威身份）移除，防止他人恶意移除。
    /// </summary>
    public async Task<LobbyResult> HandleRemovePrepareUnitAsync(string connectionId, PrepareRemoveUnitRequest req) {
        if (string.IsNullOrEmpty(req.RoomId) || string.IsNullOrEmpty(req.UnitName))
            return new LobbyResult(req.RoomId, false, "roomId and unitName required.");

        // 单位归属用服务端权威玩家名（连接归属表），不信任客户端提交，仅本人可移除
        string? ownerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (string.IsNullOrEmpty(ownerName) || !_stateStore.IsConnectionInRoom(connectionId, req.RoomId))
            return new LobbyResult(req.RoomId, false, "Player not in room.");

        bool removed = _stateStore.RemovePrepareUnit(req.RoomId, req.UnitName, req.Camp, ownerName);
        if (removed)
            await BroadcastRoomSnapshotAsync(req.RoomId);
        return new LobbyResult(req.RoomId, removed, removed ? null : "Unit not found.");
    }

    /// <summary>
    /// 处理 prepare_start_battle：仅房主可发起，且需除房主外所有玩家已准备。
    /// 校验通过后创建战斗房间服务器并向房间内所有玩家广播重定向端口。
    /// </summary>
    public async Task<LobbyResult> HandleStartBattleAsync(string connectionId, PrepareStartBattleRequest req) {
        if (string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId is required.");

        if (string.IsNullOrWhiteSpace(req.PlayerName) || req.PlayerName.Length > EntityConstants.MaxPlayerNameLength) {
            _logger.LogWarning("[Game] start_battle: invalid player name for '{PlayerId}'.", req.PlayerId);
            return new LobbyResult(req.RoomId, false, "invalid player name.");
        }

        if (!_stateStore.RoomExists(req.RoomId)) {
            _logger.LogWarning("[Game] start_battle: room '{RoomId}' not found.", req.RoomId);
            return new LobbyResult(req.RoomId, false, "Room not found.");
        }

        // 校验发起者必须是房主（基于连接归属表，不信任客户端提交的 playerName）
        if (!_stateStore.IsConnectionRoomHost(connectionId, req.RoomId)) {
            _logger.LogWarning("[Game] start_battle: connection of room '{RoomId}' is not the host, rejected.", req.RoomId);
            return new LobbyResult(req.RoomId, false, "Only room host can start battle.");
        }

        // 校验除房主外所有玩家已准备
        if (!_stateStore.IsAllOthersReady(req.RoomId)) {
            _logger.LogWarning("[Game] start_battle: room '{RoomId}' has not-ready players, rejected.", req.RoomId);
            return new LobbyResult(req.RoomId, false, "Not all players ready.");
        }

        // 创建 BattleRoomServer：初始化（根实体与单位迁移）由房间线程从 Store 自取完成
        var server = _lobby.StartRoomBattle(req.RoomId);

        // 向房间内所有玩家广播重定向（含端口号），确保非房主也能进入战斗
        await BroadcastToRoomAsync(req.RoomId, HubMethods.OnPrepareBattleRedirect,
            new RoomRedirect(req.RoomId, server.Port));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Room '{RoomId}' battle started on port {Port}.", req.RoomId, server.Port);

        return new LobbyResult(req.RoomId, true);
    }
    /// <summary>
    /// 处理 prepare_ready / prepare_unready：非房主请求设置准备状态，更新并广播房间准备状态。
    /// </summary>
    public async Task<LobbyResult> HandleSetReadyAsync(string connectionId, PrepareReadyStateRequest req) {
        if (string.IsNullOrEmpty(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId required.");

        if (!_stateStore.RoomExists(req.RoomId))
            return new LobbyResult(req.RoomId, false, "Room not found.");

        // 用连接归属反查权威玩家名（服务端 join/create 后的权威化名），
        // 避免伪造他人准备状态或使用与权威名不一致的 playerName 造成孤立键。
        string? playerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (string.IsNullOrEmpty(playerName) || !_stateStore.IsConnectionInRoom(connectionId, req.RoomId))
            return new LobbyResult(req.RoomId, false, "Player not in room.");

        _stateStore.SetPlayerReady(req.RoomId, playerName, req.Ready);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{Player}' {Action} in room '{RoomId}'.",
                playerName, req.Ready ? "ready" : "unready", req.RoomId);

        // 广播最新准备状态给房间内所有玩家
        await BroadcastRoomSnapshotAsync(req.RoomId);
        return new LobbyResult(req.RoomId, true);
    }

    /// <summary>
    /// 处理 reconnect_room：校验身份后将断线玩家重连到战斗房间。
    /// </summary>
    public async Task<LobbyResult> HandleReconnectRoomAsync(ReconnectRoomRequest req) {
        if (!ValidateServerPassword(req.ServerPassword, "ReconnectRoom", null)
            || string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId is required.");

        if (string.IsNullOrWhiteSpace(req.PlayerName))
            return new LobbyResult(req.RoomId, false, "Player name is required.");
        if (req.PlayerName.Length > EntityConstants.MaxPlayerNameLength)
            return new LobbyResult(req.RoomId, false, "Player name too long.");

        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;
        if (!_stateStore.ValidateRoomPassword(req.RoomId, actualRoomPassword))
            return new LobbyResult(req.RoomId, false, "Invalid room password.");

        var server = _lobby.GetRoomServer(req.RoomId);
        if (server == null)
            return new LobbyResult(req.RoomId, false, "Room not in battle.");

        server.UpdatePlayerName(req.PlayerId, req.PlayerName);
        server.RegisterPlayer(req.PlayerId, req.PlayerName);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Game] Player '{PlayerName}' ({PlayerId}) reconnected to room '{RoomId}' on port {Port}.",
                req.PlayerName, req.PlayerId, req.RoomId, server.Port);

        return new LobbyResult(req.RoomId, true, Port: server.Port);
    }

    /// <summary>
    /// 将房间完整状态快照（静态配置 + 准备状态 + 单位）组装后单次广播给该房间所有连接。
    /// 客户端以该快照为唯一权威视图，无需自行组装。
    /// </summary>
    private async Task BroadcastRoomSnapshotAsync(string roomId) {
        var config = _stateStore.GetRoomConfig(roomId);
        var state = _stateStore.GetRoomState(roomId);
        var units = _stateStore.GetPrepareUnits(roomId);

        var snapshot = new RoomSnapshot(
            roomId,
            config?.Title ?? roomId,
            config?.Description ?? string.Empty,
            config?.MaxPlayers ?? 2,
            config?.Status ?? RoomStatus.Waiting,
            state.HostName,
            state.DungeonName,
            config?.CurrentPlayers ?? state.Players.Count,
            [.. state.Players.Select(p => new PlayerReadyDto(p.PlayerName, p.Ready))],
            [.. units.Select(u => new PrepareUnitDto(u.UnitName, u.Camp, u.PlayerName))]);

        await BroadcastToRoomAsync(roomId, HubMethods.OnRoomSnapshot, snapshot);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Game] Broadcast room snapshot to room '{RoomId}' ({PlayerCount} players, {UnitCount} units)",
                roomId, snapshot.Players.Count, snapshot.Units.Count);
    }
}
