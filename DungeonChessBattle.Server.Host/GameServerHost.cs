using DungeonChessBattle.Server.Battle;
using DungeonChessBattle.Server.Lobby;
using DungeonChessBattle.Server.StateStore;
using DungeonChessBattle.Server.StateStore.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 游戏服务器宿主，ASP.NET Core、Kestrel 与 SignalR。
/// 负责任命名的 Kestrel 宿主、SignalR Hub 注册、依赖装配，
/// 以及空房间清理后台循环。提供 Start/Stop 操作与状态事件通知。
/// 服务器配置由 <see cref="ServerConfig"/> 唯一来源注入。
/// </summary>
public sealed class GameServerHost(ILogger<GameServerHost> logger, ILoggerFactory loggerFactory) {
    private readonly ILogger<GameServerHost> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly Lock _lock = new();
    private WebApplication? _app;
    private GameServer? _server;
    private CancellationTokenSource? _cts;
    private Task? _cleanupLoop;

    /// <summary>默认大厅端口。</summary>
    public const int DefaultPort = ServerConfig.DefaultPort;

    /// <summary>服务器是否正在运行。</summary>
    public bool IsRunning {
        get {
            lock (_lock)
                return _server != null;
        }
    }

    /// <summary>当前监听端口。</summary>
    public int Port {
        get;
        private set;
    }

    /// <summary>当前 Kestrel 宿主实例，运行中可用。</summary>
    public WebApplication? App {
        get {
            lock (_lock)
                return _app;
        }
    }

    /// <summary>服务器状态变化事件。参数：是否运行、端口。</summary>
    public event Action<bool, int>? StatusChanged;

    /// <summary>
    /// 启动服务器，Kestrel 与 SignalR，监听大厅端口。
    /// </summary>
    /// <param name="port">大厅监听端口。</param>
    /// <param name="serverPassword">服务器访问密码；为空表示不启用。</param>
    public void Start(int port = DefaultPort, string? serverPassword = null) {
        lock (_lock) {
            if (_server != null) {
                _logger.LogWarning("服务器已在运行中");
                return;
            }

            try {
                var config = ServerConfig.FromEnvironment(serverPassword) with {
                    LobbyPort = port
                };
                Port = config.LobbyPort;

                var builder = WebApplication.CreateBuilder();
                builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
                builder.Logging.AddConsole();
                builder.Services.AddSingleton(new LobbyServerConfig { ServerPassword = config.ServerPassword });
                builder.Services.AddSingleton(new BattleServerConfig {
                    ConnectionKey = config.ServerPassword ?? config.ConnectionKey,
                    FirstRoomPort = config.FirstRoomPort,
                });
                builder.Services.AddSingleton<IGameStateStore>(_ => new InMemoryGameStateStore(_loggerFactory));
                builder.Services.AddSingleton<ILobbyBroadcaster>(sp =>
                    new SignalRBroadcaster(sp.GetRequiredService<IHubContext<LobbyHub>>()));
                builder.Services.AddSingleton<GameServer>(sp => new GameServer(
                    _loggerFactory,
                    sp.GetRequiredService<ILobbyBroadcaster>(),
                    sp.GetRequiredService<LobbyServerConfig>(),
                    sp.GetRequiredService<BattleServerConfig>(),
                    sp.GetRequiredService<IGameStateStore>()));
                builder.Services.AddSingleton<ILobbyApplication>(sp => sp.GetRequiredService<GameServer>());
                builder.Services.AddSignalR();

                var app = builder.Build();
                app.MapHub<LobbyHub>("/lobby");
                app.Start();

                _app = app;
                _server = app.Services.GetRequiredService<GameServer>();

                // 空房间清理后台循环，替代原大厅轮询线程
                _cts = new CancellationTokenSource();
                _cleanupLoop = Task.Run(() => CleanupLoopAsync(_cts.Token));

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("服务器已启动，监听端口 {Port}", port);
                StatusChanged?.Invoke(true, port);
            }
            catch (Exception ex) {
                _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _app = null;
                _server = null;
                Port = 0;
                _logger.LogError(ex, "服务器启动失败");
                StatusChanged?.Invoke(false, 0);
            }
        }
    }

    /// <summary>
    /// 空房间清理循环：周期消费 <see cref="RoomServerManager.ProcessPendingRoomCleanups"/>。
    /// </summary>
    private async Task CleanupLoopAsync(CancellationToken ct) {
        using var timer = new System.Threading.PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try {
            while (await timer.WaitForNextTickAsync(ct)) {
                _server?.RoomServers.ProcessPendingRoomCleanups();
            }
        }
        catch (OperationCanceledException) {
            // 正常停止
        }
    }

    /// <summary>
    /// 停止服务器并触发状态通知。
    /// </summary>
    public void Stop() {
        lock (_lock) {
            if (_server == null) {
                _logger.LogWarning("服务器未在运行");
                return;
            }

            try {
                _cts?.Cancel();
                _cleanupLoop?.Wait(TimeSpan.FromSeconds(3));
                _server.RoomServers.StopAll();
                _app?.StopAsync().GetAwaiter().GetResult();
                _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _logger.LogInformation("服务器已停止");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "服务器停止失败");
            }
            finally {
                _cts?.Dispose();
                _cts = null;
                _cleanupLoop = null;
                _app = null;
                _server = null;
                Port = 0;
                StatusChanged?.Invoke(false, 0);
            }
        }
    }

    /// <summary>
    /// 运行控制台交互循环，支持 help / status / rooms / exit 命令。
    /// 归位到宿主层，不再属于大厅或战斗领域；当前版本不自动启动，由调用方按需启用。
    /// </summary>
    /// <param name="getPeerCount">获取当前在线人数委托。</param>
    /// <param name="getUptime">获取服务运行时长委托。</param>
    public void RunConsoleLoop(Func<int> getPeerCount, Func<TimeSpan> getUptime) {
        while (true) {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line == null)
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0].ToLowerInvariant()) {
                case "help":
                    Console.WriteLine("  rooms  |  status  |  exit");
                    break;
                case "status":
                    Console.WriteLine($"  Uptime: {getUptime():hh\\:mm\\:ss}, Clients: {getPeerCount()}");
                    break;
                case "rooms":
                    _server?.RoomServers.ListRooms();
                    break;
                case "exit":
                case "quit":
                    return;
                default:
                    Console.WriteLine($"Unknown: {parts[0]}");
                    break;
            }
        }
    }
}
