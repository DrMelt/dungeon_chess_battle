using System.Diagnostics;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;
using DungeonChessBattle.Server.Settings;
using DungeonChessBattle.Server.Stores;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。
/// 大厅端口处理 create_room / join_room / list_rooms / prepare_* / start_battle 等 JSON 消息。
/// 准备阶段在大厅连接上完成（选单位等），战斗开始时才创建 BattleRoomServer 并重定向客户端。
/// 支持服务器密码 + 房间密码两层访问控制。
/// 配置由 <see cref="ServerConfig"/> 唯一来源注入；大厅级状态数据由 <see cref="IGameStateStore"/> 持有，
/// 战斗房间生命周期由 GameLobby 协调。
/// 消息分发与各 Handle* 处理见 GameServer.MessageHandlers。
/// </summary>
public partial class GameServer {
    private readonly LobbyNetworkServer _lobbyServer;
    private readonly GameLobby _lobby;
    private readonly IGameStateStore _stateStore;
    private readonly ServerConfig _config;
    private readonly ILogger<GameServer> _logger;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();

    private volatile bool _running;
    private Thread? _lobbyThread;

    /// <summary>服务端是否正在运行。</summary>
    public bool IsRunning => _running;

    /// <summary>
    /// 初始化游戏服务端。
    /// </summary>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <param name="config">服务器配置（端口、密钥、密码）。</param>
    /// <param name="stateStore">大厅级状态存储（存储引擎由装配层注入，可替换）。</param>
    public GameServer(ILoggerFactory loggerFactory, ServerConfig config, IGameStateStore stateStore) {
        _logger = loggerFactory.CreateLogger<GameServer>();
        _config = config;
        _stateStore = stateStore;
        _lobbyServer = new LobbyNetworkServer(loggerFactory.CreateLogger<LobbyNetworkServer>(), _config);
        _lobby = new GameLobby(loggerFactory, _stateStore, _config);

        _lobbyServer.OnCustomPacket += OnCustomPacket;
        _lobbyServer.OnClientDisconnected += OnLobbyPeerDisconnected;
    }

    /// <summary>
    /// 大厅 peer 断线处理：清理该玩家所属房间的成员与准备状态，并向剩余玩家广播最新准备状态。
    /// </summary>
    private void OnLobbyPeerDisconnected(int peerId) {
        string? roomId = _stateStore.RemovePlayerByPeer(peerId);
        if (roomId == null)
            return;

        _lobby.UnregisterPeerFromRoom(roomId, peerId);
        BroadcastPrepareRoomState(roomId);
    }

    /// <summary>
    /// 异步启动服务端：启动大厅网络服务并开启后台轮询线程。
    /// </summary>
    public void StartAsync() {
        if (_running)
            return;
        _lobbyServer.Start(_config.LobbyPort);
        _running = true;

        _lobbyThread = new Thread(() => {
            while (_running) {
                _lobbyServer.PollEvents();
                // 消费空房间投递队列：房间销毁动作收敛到大厅线程执行
                _lobby.ProcessPendingRoomCleanups();
                Thread.Sleep(1);
            }
        }) {
            Name = "Lobby-Poll", IsBackground = true
        };
        _lobbyThread.Start();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[GameServer] Started, lobby port: {Port}, ServerPassword={HasPassword}",
                _config.LobbyPort, _config.ServerPassword != null);
    }

    /// <summary>
    /// 以控制台交互模式启动服务端（默认大厅端口 10170）。
    /// 运行到用户输入命令后退出循环。
    /// </summary>
    public void StartWithConsole() {
        if (_running)
            return;

        StartAsync();
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (Multi-Room)");
        Console.WriteLine("  Prepare phase stays in lobby.");
        Console.WriteLine($"  Server password: {(_config.ServerPassword != null ? "ENABLED" : "DISABLED")}");
        Console.WriteLine("  Type 'help' for commands.");
        Console.WriteLine("══════════════════════════════════════════");

        while (_running) {
            if (Console.KeyAvailable) {
                _lobby.RunConsoleLoop(() => _lobbyServer.PeerCount, () => _tickWatch.Elapsed);
                break;
            }
            Thread.Sleep(50);
        }

        _running = false;
        Stop();
    }

    /// <summary>
    /// 停止服务端：关闭轮询线程、房间与大厅网络。
    /// </summary>
    public void Stop() {
        _running = false;
        _lobbyThread?.Join(TimeSpan.FromSeconds(3));
        _lobby.StopAll();
        _lobbyServer.Stop();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Server stopped.");
    }
}
