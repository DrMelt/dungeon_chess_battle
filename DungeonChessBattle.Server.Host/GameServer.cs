using DungeonChessBattle.Protocol;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Domain.Lobby;
using DungeonChessBattle.Server.Domain.Settings;
using DungeonChessBattle.Server.State;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 游戏服务端业务协调器。
/// 大厅端口由 SignalR Hub（<see cref="LobbyHub"/>）承载，本类负责各请求的业务处理、
/// 房间生命周期协调（<see cref="GameLobby"/>）与房间内广播。
/// 业务层不依赖具体传输：广播经 <see cref="ILobbyBroadcaster"/> 端口注入实现（ASP.NET 提供）。
/// 配置由 <see cref="ServerConfig"/> 唯一来源注入；大厅级状态数据由
/// <see cref="IGameStateStore"/> 持有。各 Handle* 处理见 GameServer.MessageHandlers。
/// </summary>
/// <remarks>
/// 初始化游戏服务端业务协调器。
/// </remarks>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="broadcaster">大厅广播端口（向房间内连接推送消息）。</param>
/// <param name="config">服务器配置（端口、密钥、密码）。</param>
/// <param name="stateStore">大厅级状态存储（存储引擎由装配层注入，可替换）。</param>
public partial class GameServer(ILoggerFactory loggerFactory, ILobbyBroadcaster broadcaster,
    ServerConfig config, IGameStateStore stateStore) : ILobbyApplication {
    private readonly GameLobby _lobby = new(loggerFactory, stateStore, broadcaster, config);
    private readonly RoomServerManager _roomServers = new(loggerFactory, stateStore, config);
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly ServerConfig _config = config;
    private readonly ILogger<GameServer> _logger = loggerFactory.CreateLogger<GameServer>();
    private readonly ILobbyBroadcaster _broadcaster = broadcaster;

    /// <summary>房间服务器生命周期管理器（协调层）。</summary>
    internal RoomServerManager RoomServers => _roomServers;

    /// <summary>大厅级状态存储。</summary>
    internal IGameStateStore StateStore => _stateStore;

    /// <summary>服务器配置。</summary>
    internal ServerConfig Config => _config;

    /// <summary>
    /// 向房间内所有成员连接广播消息。
    /// </summary>
    private async Task BroadcastToRoomAsync<TDto>(string roomId, string hubMethod, TDto dto) {
        await _broadcaster.SendToRoomAsync(roomId, hubMethod, dto);
    }

    /// <summary>
    /// 连接断开清理：移除该连接所属房间的成员与准备状态，并向剩余玩家广播最新房间快照。
    /// </summary>
    public async Task ConnectionLostAsync(string connectionId) {
        string? roomId = _stateStore.RemovePlayerByConnection(connectionId);
        if (roomId == null)
            return;

        await _lobby.BroadcastRoomSnapshotAsync(roomId);
    }
}
