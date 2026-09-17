using DungeonChessBattle.Lobby.Shared;
using DungeonChessBattle.Lobby.Protocol;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Lobby.Protocol.Dtos;
using DungeonChessBattle.Battle.Server.Shared;
using DungeonChessBattle.Server.DataStore.Shared;
using DungeonChessBattle.Session.Shared;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Battle.Config.Shared;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// 大厅业务协调者，Server.Lobby：处理大厅 SignalR 协议的各类业务请求，
/// 包括创建/加入房间、招募板列表、准备单位增删、准备状态设置与房间快照广播。
/// 所有大厅级状态数据，房间配置、密码、玩家准备状态与准备单位等，统一由
/// <see cref="IGameStateStore"/> 持有，本类不存储业务状态。
/// 向客户端广播经 <see cref="SignalRBroadcaster"/> 注入实现，不依赖具体传输。
/// 战斗房间服务器的生命周期管理由协调层经 <see cref="IBattleRoomManager"/> 接口编排，
/// 本类不触碰战斗房间。
/// </summary>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="stateStore">大厅级状态存储。</param>
/// <param name="broadcaster">大厅广播端口，向房间内连接推送消息。</param>
/// <param name="config">服务器配置，服务器密码等。</param>
/// <param name="content">内容注册表只读视图，阵营选项、单位配置、副本键与内容修订号来源。</param>
public class GameLobby(ILoggerFactory loggerFactory, IGameStateStore stateStore,
    SignalRBroadcaster broadcaster, LobbyServerConfig config,
    IContentRegistryView content) {
    private readonly ILogger<GameLobby> _logger = loggerFactory.CreateLogger<GameLobby>();
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly SignalRBroadcaster _broadcaster = broadcaster;
    private readonly LobbyServerConfig _config = config;
    private readonly IContentRegistryView _content = content;

    /// <summary>
    /// 校验服务器密码；不匹配时返回 false，调用方负责构造失败结果。
    /// </summary>
    private bool ValidateServerPassword(string? serverPassword, string responseDesc, RoomId roomId) {
        if (!string.IsNullOrEmpty(_config.ServerPassword) && serverPassword != _config.ServerPassword) {
            _logger.LogWarning("{Desc}: invalid server password (room '{RoomId}').", responseDesc, roomId);
            return false;
        }
        return true;
    }

    /// <summary>解析建房选定的副本键：键缺失、非法或未注册返回 null，由调用方拒绝建房。</summary>
    /// <param name="dungeonKey">客户端选定的副本键，必填。</param>
    /// <returns>权威副本键；无合法键时为 null。</returns>
    private string? ResolveSelectedDungeonKey(string? dungeonKey) {
        if (RestrictedString.TryCreate(dungeonKey, DungeonKeyId.MaxLength) is not { } key)
            return null;
        return _content.GetDungeon(key.Value)?.DungeonKey.Value;
    }

    /// <summary>解析房间已持久化的副本键：空或解析不到即房间指向已消失的副本，响亮失败。</summary>
    /// <param name="dungeonKey">房间快照里的副本键。</param>
    private string ResolveStoredDungeonKey(string? dungeonKey) =>
        string.IsNullOrEmpty(dungeonKey)
            ? throw new InvalidOperationException("Room has no dungeon key.")
            : _content.GetDungeon(dungeonKey)?.DungeonKey
                ?? throw new InvalidOperationException($"Room references unknown dungeon key '{dungeonKey}'.");

    /// <summary>
    /// 处理 login：登记连接为登录会话，玩家名成为服务端权威身份，并为其签发会话凭证。
    /// 名字非法时拒绝；会话内业务从登录会话反查身份，会话凭证让身份延伸到服务端 HTTP 端点。
    /// </summary>
    public Task<LoginResult> HandleLoginAsync(string connectionId, LoginRequest req) {
        if (!_stateStore.TryRegisterLoginSession(connectionId, req.PlayerName))
            return Task.FromResult(new LoginResult(false, Error: "Invalid player name."));
        return Task.FromResult(new LoginResult(true, req.PlayerName,
            SessionToken: _stateStore.IssueSessionToken(connectionId)));
    }

    /// <summary>
    /// 处理 create_room：注册房间，准备阶段不重定向。
    /// </summary>
    public async Task<LobbyResult> HandleCreateRoomAsync(string connectionId, CreateRoomRequest req) {
        if (!ValidateServerPassword(req.ServerPassword, "CreateRoom", RoomId.None))
            return new LobbyResult(string.Empty, false, "invalid server password.");

        // 房主名从登录会话取服务端权威身份，不信任客户端提交
        string? hostDisplayName = _stateStore.GetLoginPlayerName(connectionId);
        if (string.IsNullOrEmpty(hostDisplayName))
            return new LobbyResult(string.Empty, false, "Player not logged in.");

        // 房间 ID 由服务端权威生成，客户端不提交，避免碰撞与伪造
        RoomId roomId = Guid.NewGuid().ToString("N");
        string playerId = req.PlayerId;
        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;

        // 协议字段在边界上仍需兜底：反序列化不受可空标注约束，缺失配置即为非法请求
        if (req.Config is null)
            return new LobbyResult(string.Empty, false, "room config required.");

        // 副本键由服务端权威解析：客户端提交的键超长或未注册即拒绝建房，不静默回落
        string? dungeonKey = ResolveSelectedDungeonKey(req.Config.DungeonKey);
        if (dungeonKey is null)
            return new LobbyResult(string.Empty, false, "invalid dungeon key.");

        GameRoom roomConfig = new(roomId) {
            DungeonKey = dungeonKey,
            Description = req.Config.Description,
            HostName = hostDisplayName,
            MaxPlayers = req.Config.MaxPlayers > 0 ? req.Config.MaxPlayers : 2,
            CurrentPlayers = 1,
            // 房间携带服务端当前内容指纹，客户端不一致拒绝加入，保证内容同源
            ContentFingerprint = _content.DataRevision
        };

        // 组合原子注册：单锁内完成房间注册 + 房主登记 + 成员登记 + 连接归属 + playerId
        if (!_stateStore.TryRegisterRoomWithHost(roomId, actualRoomPassword, roomConfig,
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
        if (!ValidateServerPassword(req.ServerPassword, "JoinRoom", RoomId.None))
            return new LobbyResult(req.RoomId, false, "invalid server password.");

        // 客户端提交的房间 ID 先过值对象判定：空与超长都按非法请求拒绝，不让转换校验的异常冒出去
        if (RoomId.TryCreate(req.RoomId) is not { } roomId)
            return new LobbyResult(req.RoomId, false, "invalid roomId.");

        // 仅允许加入等待中的房间；进行中和已结束的房间不可加入
        var roomConfig = _stateStore.GetRoomConfig(roomId);
        if (roomConfig == null)
            return new LobbyResult(roomId, false, "Room not found.");
        if (roomConfig.Status != RoomStatus.Waiting)
            return new LobbyResult(roomId, false, "Room is not available for joining.");

        string? actualRoomPassword = string.IsNullOrEmpty(req.RoomPassword) ? null : req.RoomPassword;
        if (!_stateStore.ValidateRoomPassword(roomId, actualRoomPassword))
            return new LobbyResult(roomId, false, "Invalid room password.");

        // 玩家名从登录会话取服务端权威身份，不信任客户端提交；先校验再改状态，失败不留脏状态
        string? displayName = _stateStore.GetLoginPlayerName(connectionId);
        if (string.IsNullOrEmpty(displayName))
            return new LobbyResult(roomId, false, "Player not logged in.");

        // 原子自增玩家数，避免并发 join 时读改写丢失更新
        _stateStore.IncrementPlayerCount(roomId);
        await _broadcaster.AddToRoomAsync(connectionId, roomId);

        // 登记玩家为房间准备成员，默认未准备，playerId 一并登记用于战斗白名单
        _stateStore.RegisterRoomPlayer(roomId, displayName, req.PlayerId, connectionId);

        await BroadcastRoomSnapshotAsync(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player '{Player}' ({PlayerId}) joined room '{RoomId}' (prepare).",
                displayName, req.PlayerId, roomId);

        return new LobbyResult(roomId, true);
    }

    /// <summary>
    /// 处理 list_rooms：返回招募板房间列表。
    /// 招募板仅展示等待中的房间；进行中和已结束的房间对大厅隐藏。
    /// </summary>
    public Task<RoomListResult> HandleListRoomsAsync() {
        var rooms = _stateStore.ListActiveRooms()
            .Where(r => r.Status == RoomStatus.Waiting)
            .Select(r => new RoomListing {
                RoomId = r.RoomId,
                DungeonKey = r.DungeonKey,
                Description = r.Description,
                HostName = r.HostName,
                CurrentPlayers = r.CurrentPlayers,
                MaxPlayers = r.MaxPlayers,
                HasPassword = r.HasPassword,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ContentFingerprint = r.ContentFingerprint,
            })
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

        if (req.UnitConfigKey.Length > UnitConfigKey.MaxLength || string.IsNullOrEmpty(req.CampOptionKey))
            return new LobbyResult(string.Empty, false, "Invalid unit params.");

        RoomId roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? ownerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId.IsDefault || ownerName == null)
            return new LobbyResult(string.Empty, false, "Player not in room.");

        // 反查该玩家的持久 playerId，控制器绑定用权威键，与连接密钥一致
        string? ownerPlayerId = _stateStore.GetRoomPlayerIds(roomId).GetValueOrDefault(ownerName);
        if (string.IsNullOrEmpty(ownerPlayerId))
            return new LobbyResult(roomId, false, "Player identity not registered.");

        // 阵营由副本配置权威解析：客户端只提交选项键，不直接设置阵营数组
        var roomConfig = _stateStore.GetRoomConfig(roomId);
        var dungeon = roomConfig == null ? null : _content.GetDungeon(roomConfig.DungeonKey);
        var campOption = dungeon?.PlayerCampOptions.FirstOrDefault(o => o.Key == req.CampOptionKey);
        if (campOption == null)
            return new LobbyResult(roomId, false, "Invalid camp option.");

        // 单位必须在玩家可选单位名册内：名册由内容注册写入，虚构键与不可选单位同源拒绝
        if (!_content.IsPlayerSelectable(req.UnitConfigKey))
            return new LobbyResult(roomId, false, "Invalid unit config.");

        if (!_stateStore.AddPrepareUnit(roomId, req.UnitConfigKey, req.CampOptionKey, ownerName, ownerPlayerId))
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

        RoomId roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? ownerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId.IsDefault || string.IsNullOrEmpty(ownerName))
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
        RoomId roomId = _stateStore.GetRoomIdForConnection(connectionId);
        string? playerName = _stateStore.GetPlayerNameForConnection(connectionId);
        if (roomId.IsDefault || string.IsNullOrEmpty(playerName))
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
    public async Task BroadcastRoomSnapshotAsync(RoomId roomId) {
        var roomConfig = _stateStore.GetRoomConfig(roomId);
        var state = _stateStore.GetRoomState(roomId);
        var units = _stateStore.GetPrepareUnits(roomId);

        var snapshot = new RoomSnapshot(
            roomId,
            roomConfig?.Description ?? string.Empty,
            roomConfig?.MaxPlayers ?? 2,
            roomConfig?.Status ?? RoomStatus.Waiting,
            state.HostName,
            ResolveStoredDungeonKey(state.DungeonKey),
            roomConfig?.CurrentPlayers ?? state.Players.Count,
            [.. state.Players.Select(p => new PlayerReadyDto(p.PlayerName, p.Ready))],
            [.. units.Select(u => new PrepareUnitDto(u.UnitConfigKey, u.CampOptionKey, u.PlayerName))],
            roomConfig?.ContentFingerprint ?? string.Empty);

        await _broadcaster.SendToRoomAsync(roomId, HubMethods.OnRoomSnapshot, snapshot);

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Broadcast room snapshot to room '{RoomId}' ({PlayerCount} players, {UnitCount} units)",
                roomId, snapshot.Players.Count, snapshot.Units.Count);
    }
}
