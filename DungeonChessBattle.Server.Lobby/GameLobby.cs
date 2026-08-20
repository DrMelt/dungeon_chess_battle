using DungeonChessBattle.Protocol;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Server.StateStore.Abstractions;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅业务协调者，Server.Lobby：处理大厅 SignalR 协议的各类业务请求，
/// 包括创建/加入房间、招募板列表、准备单位增删、准备状态设置与房间快照广播。
/// 所有大厅级状态数据，房间配置、密码、玩家准备状态与准备单位等，统一由
/// <see cref="IGameStateStore"/> 持有，本类不存储业务状态。
/// 向客户端广播经 <see cref="ILobbyBroadcaster"/> 端口注入实现，不依赖具体传输。
/// 战斗房间服务器的生命周期管理由协调层经 <see cref="IBattleRoomManager"/> 契约编排，
/// 本类不触碰战斗房间。
/// </summary>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="stateStore">大厅级状态存储。</param>
/// <param name="broadcaster">大厅广播端口，向房间内连接推送消息。</param>
/// <param name="config">服务器配置，服务器密码等。</param>
public class GameLobby(ILoggerFactory loggerFactory, IGameStateStore stateStore,
    ILobbyBroadcaster broadcaster, LobbyServerConfig config) {
    private readonly ILogger<GameLobby> _logger = loggerFactory.CreateLogger<GameLobby>();
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly ILobbyBroadcaster _broadcaster = broadcaster;
    private readonly LobbyServerConfig _config = config;

    /// <summary>
    /// 校验服务器密码；不匹配时返回 false，调用方负责构造失败结果。
    /// </summary>
    private bool ValidateServerPassword(string? serverPassword, string responseDesc, string? roomId) {
        if (!string.IsNullOrEmpty(_config.ServerPassword) && serverPassword != _config.ServerPassword) {
            _logger.LogWarning("{Desc}: invalid server password (room '{RoomId}').", responseDesc, roomId);
            return false;
        }
        return true;
    }

    /// <summary>解析权威玩家显示名：空或超长时退化为 Player_{playerId 前 6 位}。</summary>
    private static string GetDisplayName(string? playerName, string playerId) {
        if (playerName == null)
            return $"Player_{playerId[..Math.Min(playerId.Length, 6)]}";
        // 超长拒绝，安全优于截断，避免两个玩家显示名碰撞
        return playerName.Length <= EntityConstants.MaxPlayerNameLength
            ? playerName
            : $"Player_{playerId[..Math.Min(playerId.Length, 6)]}";
    }

    /// <summary>解析权威副本键：非法键回落默认副本。</summary>
    /// <param name="dungeonKey">客户端提交的副本键。</param>
    /// <returns>合法的副本键。</returns>
    public static string ResolveDungeonKey(string? dungeonKey) {
        var info = DungeonRegistry.Instance.GetByKey(dungeonKey);
        return info?.DungeonKey ?? EntityConstants.DefaultDungeonKey;
    }

    /// <summary>
    /// 处理 create_room：注册房间，准备阶段不重定向。
    /// </summary>
    public async Task<LobbyResult> HandleCreateRoomAsync(string connectionId, CreateRoomRequest req) {
        if (!ValidateServerPassword(req.ServerPassword, "CreateRoom", null))
            return new LobbyResult(string.Empty, false, "invalid server password.");

        // 房间 ID 由服务端权威生成，客户端不提交，避免碰撞与伪造
        string roomId = Guid.NewGuid().ToString("N");
        string playerId = req.PlayerId;
        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;

        // 房主 displayName 由服务端权威解析，不信任客户端提交的 HostName
        string hostDisplayName = GetDisplayName(req.PlayerName, playerId);

        GameRoom config;
        if (req.Config != null) {
            config = new GameRoom(roomId) {
                DungeonKey = ResolveDungeonKey(req.Config.DungeonKey),
                Description = req.Config.Description,
                HostName = hostDisplayName,
                MaxPlayers = req.Config.MaxPlayers > 0 ? req.Config.MaxPlayers : 2,
                CurrentPlayers = 1,
            };
        }
        else {
            // 启用默认值填充房间，无配置直接进入战斗
            config = new GameRoom(roomId) {
                DungeonKey = EntityConstants.DefaultDungeonKey,
                HostName = hostDisplayName,
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
        }

        // 组合原子注册：单锁内完成房间注册 + 房主登记 + 成员登记 + 连接归属 + playerId
        if (!_stateStore.TryRegisterRoomWithHost(roomId, actualRoomPassword, config,
                hostDisplayName, playerId, connectionId))
            return new LobbyResult(roomId, false, "Failed to register room.");

        // 加入房间连接分组，准备阶段广播用
        await _broadcaster.AddToRoomAsync(connectionId, roomId);

        await BroadcastRoomSnapshotAsync(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room '{RoomId}' created (prepare), player='{Player}' ({PlayerId}).",
                roomId, hostDisplayName, playerId);

        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 处理 join_room：验证房间与密码，准备阶段不重定向。
    /// </summary>
    public async Task<LobbyResult> HandleJoinRoomAsync(string connectionId, JoinRoomRequest req) {
        if (!ValidateServerPassword(req.ServerPassword, "JoinRoom", null)
            || string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId is required.");

        // 仅允许加入等待中的房间；进行中和已结束的房间不可加入
        var roomConfig = _stateStore.GetRoomConfig(req.RoomId);
        if (roomConfig == null)
            return new LobbyResult(req.RoomId, false, "Room not found.");
        if (roomConfig.Status != RoomStatus.Waiting)
            return new LobbyResult(req.RoomId, false, "Room is not available for joining.");

        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;
        if (!_stateStore.ValidateRoomPassword(req.RoomId, actualRoomPassword))
            return new LobbyResult(req.RoomId, false, "Invalid room password.");

        // 原子自增玩家数，避免并发 join 时读改写丢失更新
        _stateStore.IncrementPlayerCount(req.RoomId);
        await _broadcaster.AddToRoomAsync(connectionId, req.RoomId);

        string displayName = GetDisplayName(req.PlayerName, req.PlayerId);
        // 登记玩家为房间准备成员，默认未准备，playerId 一并登记用于战斗白名单
        _stateStore.RegisterRoomPlayer(req.RoomId, displayName, req.PlayerId, connectionId);

        await BroadcastRoomSnapshotAsync(req.RoomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player '{Player}' ({PlayerId}) joined room '{RoomId}' (prepare).",
                displayName, req.PlayerId, req.RoomId);

        return new LobbyResult(req.RoomId, true);
    }

    /// <summary>
    /// 处理 list_rooms：返回招募板房间列表。
    /// 招募板仅展示等待中的房间；进行中和已结束的房间对大厅隐藏。
    /// </summary>
    public Task<RoomListResult> HandleListRoomsAsync() {
        var rooms = _stateStore.ListActiveRooms()
            .Where(r => r.Status == RoomStatus.Waiting)
            .ToList();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Sent listing of {Count} rooms.", rooms.Count);
        return Task.FromResult(new RoomListResult(rooms));
    }

    /// <summary>
    /// 处理 prepare_add_unit：为房间添加准备单位，并广播最新列表。
    /// 房间与单位归属从连接归属反查；阵营由房间所选副本配置按选项键权威解析，不信任客户端提交的阵营。
    /// </summary>
    public async Task<LobbyResult> HandleAddPrepareUnitAsync(string connectionId, PrepareAddUnitRequest req) {
        if (string.IsNullOrEmpty(req.UnitConfigKey))
            return new LobbyResult(string.Empty, false, "unitConfigKey required.");

        if (req.UnitConfigKey.Length > EntityConstants.MaxUnitConfigKeyLength || string.IsNullOrEmpty(req.CampOptionKey))
            return new LobbyResult(string.Empty, false, "Invalid unit params.");

        string? roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? ownerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId == null || ownerName == null)
            return new LobbyResult(string.Empty, false, "Player not in room.");

        // 反查该玩家的持久 playerId，控制器绑定用权威键，与连接密钥一致
        string? ownerPlayerId = _stateStore.GetRoomPlayerIds(roomId).GetValueOrDefault(ownerName);
        if (string.IsNullOrEmpty(ownerPlayerId))
            return new LobbyResult(roomId, false, "Player identity not registered.");

        // 阵营由副本配置权威解析：客户端只提交选项键，不直接设置阵营数组
        var roomConfig = _stateStore.GetRoomConfig(roomId);
        var dungeon = roomConfig == null ? null : DungeonRegistry.Instance.GetByKey(roomConfig.DungeonKey);
        var campOption = dungeon?.PlayerCampOptions.FirstOrDefault(o => o.Key == req.CampOptionKey);
        if (campOption == null)
            return new LobbyResult(roomId, false, "Invalid camp option.");

        // 单位必须为已注册且可被玩家选择的配置，拒绝虚构键与敌人单位
        var unitConfig = UnitRegistry.Instance.GetByKey(req.UnitConfigKey);
        if (unitConfig == null || !unitConfig.IsPlayerSelectable)
            return new LobbyResult(roomId, false, "Invalid unit config.");

        if (!_stateStore.AddPrepareUnit(roomId, req.UnitConfigKey, req.CampOptionKey, campOption.Camps, ownerName, ownerPlayerId))
            return new LobbyResult(roomId, false,
                _stateStore.RoomExists(roomId) ? "Cannot change unit while ready." : "Room not found.");

        // 广播更新给房间内所有玩家
        await BroadcastRoomSnapshotAsync(roomId);
        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 处理 prepare_remove_unit：从房间移除准备单位，成功时广播最新列表。
    /// 房间与单位归属均从连接归属反查，仅归属者可移除，防止他人恶意移除。
    /// </summary>
    public async Task<LobbyResult> HandleRemovePrepareUnitAsync(string connectionId, PrepareRemoveUnitRequest req) {
        if (string.IsNullOrEmpty(req.UnitConfigKey))
            return new LobbyResult(string.Empty, false, "unitConfigKey required.");

        string? roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? ownerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId == null || string.IsNullOrEmpty(ownerName))
            return new LobbyResult(string.Empty, false, "Player not in room.");

        bool removed = _stateStore.RemovePrepareUnit(roomId, req.UnitConfigKey, ownerName);
        if (removed) {
            await BroadcastRoomSnapshotAsync(roomId);
            return new LobbyResult(roomId, true);
        }

        // 已准备的玩家不能移除角色；否则视为单位不存在
        string error = _stateStore.IsPlayerReady(roomId, ownerName)
            ? "Cannot change unit while ready."
            : "Unit not found.";
        return new LobbyResult(roomId, false, error);
    }

    /// <summary>
    /// 处理 prepare_ready / prepare_unready：非房主请求设置准备状态，更新并广播房间准备状态。
    /// 房间与权威玩家名均从连接归属反查，避免伪造他人准备状态或使用不一致的玩家名造成孤立键。
    /// </summary>
    public async Task<LobbyResult> HandleSetReadyAsync(string connectionId, PrepareReadyStateRequest req) {
        string? roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? playerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId == null || string.IsNullOrEmpty(playerName))
            return new LobbyResult(string.Empty, false, "Player not in room.");

        if (!_stateStore.RoomExists(roomId))
            return new LobbyResult(roomId, false, "Room not found.");

        // 房主不参与准备
        if (_stateStore.IsConnectionRoomHost(connectionId, roomId))
            return new LobbyResult(roomId, false, "Host cannot set ready state.");

        // 未选择角色不能准备
        if (!_stateStore.TrySetPlayerReady(roomId, playerName, req.Ready)) {
            _logger.LogWarning("Player '{Player}' set_ready rejected in room '{RoomId}' (ready={Ready}).",
                playerName, roomId, req.Ready);
            return new LobbyResult(roomId, false, "Select a unit before ready.");
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player '{Player}' {Action} in room '{RoomId}'.",
                playerName, req.Ready ? "ready" : "unready", roomId);

        // 广播最新准备状态给房间内所有玩家
        await BroadcastRoomSnapshotAsync(roomId);
        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 将房间完整状态快照，静态配置、准备状态与单位，组装后单次广播给该房间所有连接。
    /// 客户端以该快照为唯一权威视图，无需自行组装。
    /// </summary>
    public async Task BroadcastRoomSnapshotAsync(string roomId) {
        var config = _stateStore.GetRoomConfig(roomId);
        var state = _stateStore.GetRoomState(roomId);
        var units = _stateStore.GetPrepareUnits(roomId);

        var snapshot = new RoomSnapshot(
            roomId,
            config?.Description ?? string.Empty,
            config?.MaxPlayers ?? 2,
            config?.Status ?? RoomStatus.Waiting,
            state.HostName,
            DungeonRegistry.Instance.GetByKey(state.DungeonKey)?.DungeonKey ?? EntityConstants.DefaultDungeonKey,
            config?.CurrentPlayers ?? state.Players.Count,
            [.. state.Players.Select(p => new PlayerReadyDto(p.PlayerName, p.Ready))],
            [.. units.Select(u => new PrepareUnitDto(u.UnitConfigKey, u.CampOptionKey, u.PlayerName))]);

        await _broadcaster.SendToRoomAsync(roomId, HubMethods.OnRoomSnapshot, snapshot);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Broadcast room snapshot to room '{RoomId}' ({PlayerCount} players, {UnitCount} units)",
                roomId, snapshot.Players.Count, snapshot.Units.Count);
    }
}
