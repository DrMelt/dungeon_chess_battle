using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Server.StateStore.Abstractions;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 游戏服务端业务协调器，大厅服务器门面。
/// 大厅端口由 SignalR Hub <see cref="LobbyHub"/> 承载，本类负责各请求的业务处理、
/// 房间生命周期协调与房间内广播。
/// 业务层不依赖具体传输：广播经 <see cref="ILobbyBroadcaster"/> 端口注入实现，由传输层提供。
/// 配置由装配层映射后的职责切片 <see cref="LobbyServerConfig"/> 注入；
/// 战斗房间域名只经 <see cref="IRoomServerManager"/> 契约编排，不感知实现；
/// 大厅级状态数据由 <see cref="IGameStateStore"/> 持有，各 Handle* 处理见 GameServer.MessageHandlers。
/// </summary>
/// <remarks>
/// 初始化游戏服务端业务协调器。
/// </remarks>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="broadcaster">大厅广播端口，向房间内连接推送消息。</param>
/// <param name="lobbyConfig">大厅侧配置切片，服务器密码等。</param>
/// <param name="roomServers">战斗房间服务器生命周期契约，由装配层绑定实现。</param>
/// <param name="stateStore">大厅级状态存储，存储引擎由装配层注入，可替换。</param>
public partial class GameServer(ILoggerFactory loggerFactory, ILobbyBroadcaster broadcaster,
    LobbyServerConfig lobbyConfig, IRoomServerManager roomServers, IGameStateStore stateStore) : ILobbyApplication {
    private readonly GameLobby _lobby = new(loggerFactory, stateStore, broadcaster, lobbyConfig);
    private readonly IRoomServerManager _roomServers = roomServers;
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly ILogger<GameServer> _logger = loggerFactory.CreateLogger<GameServer>();
    private readonly ILobbyBroadcaster _broadcaster = broadcaster;

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
    /// 房间从连接归属反查；先从房间广播分组移除连接，再复用统一清理
    /// <see cref="IPlayerStateStore.RemovePlayerByConnection"/>
    /// 成员、单位、人数、房主转让与空房删除，并向剩余玩家广播最新房间快照。
    /// </summary>
    public async Task<LobbyResult> HandleLeaveRoomAsync(string connectionId) {
        string? roomId = _stateStore.GetRoomIdForConnection(connectionId);
        if (roomId == null)
            return new LobbyResult(string.Empty, false, "Player not in room.");

        // 先停止接收该房间广播，再清理状态，清理后最后一人退出时房间已删，无需广播
        await _broadcaster.RemoveFromRoomAsync(connectionId, roomId);

        string? removedRoomId = _stateStore.RemovePlayerByConnection(connectionId);
        if (removedRoomId == null)
            return new LobbyResult(roomId, true); // 最后一人退出，房间已删除

        await _lobby.BroadcastRoomSnapshotAsync(removedRoomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Player left room '{RoomId}'.", removedRoomId);

        return new LobbyResult(roomId, true);
    }
}
