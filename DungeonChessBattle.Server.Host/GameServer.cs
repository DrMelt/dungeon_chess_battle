using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Server.Battle;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.StateStore.Abstractions;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 游戏服务端业务协调器。
/// 大厅端口由 SignalR Hub <see cref="LobbyHub"/> 承载，本类负责各请求的业务处理、
/// 房间生命周期协调 <see cref="GameLobby"/> 与房间内广播。
/// 业务层不依赖具体传输：广播经 <see cref="ILobbyBroadcaster"/> 端口注入实现，ASP.NET 提供。
/// 配置由装配层映射后的职责切片 <see cref="LobbyServerConfig"/> 与 <see cref="BattleServerConfig"/> 注入；
/// 大厅级状态数据由 <see cref="IGameStateStore"/> 持有。各 Handle* 处理见 GameServer.MessageHandlers。
/// </summary>
/// <remarks>
/// 初始化游戏服务端业务协调器。
/// </remarks>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="broadcaster">大厅广播端口，向房间内连接推送消息。</param>
/// <param name="lobbyConfig">大厅侧配置切片，服务器密码等。</param>
/// <param name="battleConfig">战斗侧配置切片，连接密钥与房间端口池起点。</param>
/// <param name="stateStore">大厅级状态存储，存储引擎由装配层注入，可替换。</param>
public partial class GameServer(ILoggerFactory loggerFactory, ILobbyBroadcaster broadcaster,
    LobbyServerConfig lobbyConfig, BattleServerConfig battleConfig, IGameStateStore stateStore) : ILobbyApplication {
    private readonly GameLobby _lobby = new(loggerFactory, stateStore, broadcaster, lobbyConfig);
    private readonly RoomServerManager _roomServers = new(loggerFactory, stateStore, battleConfig);
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly ILogger<GameServer> _logger = loggerFactory.CreateLogger<GameServer>();
    private readonly ILobbyBroadcaster _broadcaster = broadcaster;

    /// <summary>房间服务器生命周期管理器，协调层。</summary>
    internal RoomServerManager RoomServers => _roomServers;

    /// <summary>大厅级状态存储。</summary>
    internal IGameStateStore StateStore => _stateStore;

    /// <summary>
    /// 向房间内所有成员连接广播消息。
    /// </summary>
    private async Task BroadcastToRoomAsync<TDto>(string roomId, string hubMethod, TDto dto) {
        await _broadcaster.SendToRoomAsync(roomId, hubMethod, dto);
    }

    /// <summary>
    /// 连接断开清理：移除该连接所属房间的成员与准备状态，并向剩余玩家广播最新房间快照。
    /// 准备阶段房间的最后一人退出时房间被删除，本方法不再广播。
    /// </summary>
    public async Task ConnectionLostAsync(string connectionId) {
        string? roomId = _stateStore.RemovePlayerByConnection(connectionId);
        if (roomId == null)
            return;

        await _lobby.BroadcastRoomSnapshotAsync(roomId);
    }

    /// <summary>
    /// 处理 leave_room：玩家主动离开房间，准备阶段退出。
    /// 先从房间广播分组移除连接，再复用统一清理
    /// <see cref="IPlayerStateStore.RemovePlayerByConnection"/>
    /// 成员、单位、人数、房主转让与空房删除，并向剩余玩家广播最新房间快照。
    /// </summary>
    public async Task<LobbyResult> HandleLeaveRoomAsync(string connectionId, LeaveRoomRequest req) {
        if (string.IsNullOrWhiteSpace(req.RoomId))
            return new LobbyResult(req.RoomId, false, "roomId is required.");

        // 先停止接收该房间广播，再清理状态，清理后最后一人退出时房间已删，无需广播
        await _broadcaster.RemoveFromRoomAsync(connectionId, req.RoomId);

        string? roomId = _stateStore.RemovePlayerByConnection(connectionId);
        if (roomId == null)
            return new LobbyResult(req.RoomId, true); // 最后一人退出，房间已删除

        await _lobby.BroadcastRoomSnapshotAsync(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player left room '{RoomId}'.", req.RoomId);

        return new LobbyResult(req.RoomId, true);
    }
}
