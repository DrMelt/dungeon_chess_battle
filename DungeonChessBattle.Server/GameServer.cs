using System.Diagnostics;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务端主控类。
/// 大厅端口 (10170) 处理 create_room / join_room / list_rooms / prepare_* / start_battle 等 JSON 消息。
/// 准备阶段在大厅连接上完成（选单位等），战斗开始时才创建 BattleRoomServer 并重定向客户端。
/// 支持服务器密码 + 房间密码两层访问控制。
/// 消息分发与各 Handle* 处理见 GameServer.MessageHandlers。
/// </summary>
public partial class GameServer {
    private readonly LobbyNetworkServer _lobbyServer;
    private readonly GameLobby _lobby;
    private readonly ILogger<GameServer> _logger;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private readonly string? _serverPassword;

    private volatile bool _running;
    private Thread? _lobbyThread;

    /// <summary>服务端是否正在运行。</summary>
    public bool IsRunning => _running;

    /// <summary>
    /// 初始化游戏服务端。
    /// </summary>
    /// <param name="loggerFactory">日志工厂。</param>
    /// <param name="serverPassword">服务器访问密码；为空表示不启用。</param>
    public GameServer(ILoggerFactory loggerFactory, string? serverPassword = null) {
        _logger = loggerFactory.CreateLogger<GameServer>();
        _serverPassword = string.IsNullOrEmpty(serverPassword) ? null : serverPassword;
        _lobbyServer = new LobbyNetworkServer(loggerFactory.CreateLogger<LobbyNetworkServer>(), _serverPassword);
        _lobby = new GameLobby(loggerFactory);

        _lobbyServer.OnCustomPacket += OnCustomPacket;
        _lobbyServer.OnClientDisconnected += OnLobbyPeerDisconnected;
    }

    /// <summary>
    /// 大厅 peer 断线处理：清理该玩家所属房间的成员与准备状态，并向剩余玩家广播最新准备状态。
    /// </summary>
    private void OnLobbyPeerDisconnected(int peerId) {
        string? roomId = _lobby.RemovePlayerFromRoom(peerId);
        if (roomId == null)
            return;

        _lobby.UnregisterPeerFromRoom(roomId, peerId);
        BroadcastPrepareRoomState(roomId);
    }

    /// <summary>
    /// 异步启动服务端：启动大厅网络服务并开启后台轮询线程。
    /// </summary>
    /// <param name="lobbyPort">大厅监听端口。</param>
    public void StartAsync(int lobbyPort) {
        if (_running)
            return;
        _lobbyServer.Start(lobbyPort);
        _running = true;

        _lobbyThread = new Thread(() => {
            while (_running) {
                _lobbyServer.PollEvents();
                Thread.Sleep(1);
            }
        }) {
            Name = "Lobby-Poll", IsBackground = true
        };
        _lobbyThread.Start();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[GameServer] Started, lobby port: {Port}, ServerPassword={HasPassword}", lobbyPort, _serverPassword != null);
    }

    /// <summary>
    /// 以控制台交互模式启动服务端（默认大厅端口 10170）。
    /// 运行到用户输入命令后退出循环。
    /// </summary>
    public void StartWithConsole() {
        if (_running)
            return;

        var password = _serverPassword ?? Environment.GetEnvironmentVariable("DCB_SERVER_PASSWORD");
        if (!string.IsNullOrEmpty(password) && _serverPassword == null) {
            _logger.LogWarning("[GameServer] Server password from env but LobbyNetworkServer already created without it. Restart required.");
        }

        StartAsync(10170);
        Console.WriteLine("══════════════════════════════════════════");
        Console.WriteLine("  DungeonChessBattle Server (Multi-Room)");
        Console.WriteLine("  Prepare phase stays in lobby.");
        Console.WriteLine($"  Server password: {(_serverPassword != null ? "ENABLED" : "DISABLED")}");
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
