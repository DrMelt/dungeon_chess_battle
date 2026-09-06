using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Lobby.Protocol;
using DungeonChessBattle.Lobby.Server;
using DungeonChessBattle.Replay.Server;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 游戏服务器宿主：负责 Kestrel 宿主、SignalR Hub 注册与生命周期编排。
/// DI 装配归 <see cref="ServerHostServiceExtensions"/>，空房间清理循环归 <see cref="RoomCleanupLoop"/>，
/// 配置由 <see cref="ServerConfig"/> 唯一来源注入。提供 Start/Stop/RunAsync 操作。
/// </summary>
public sealed class GameServerHost(ILoggerFactory loggerFactory, ServerConfig config) {
    private readonly ILogger<GameServerHost> _logger = loggerFactory.CreateLogger<GameServerHost>();
    private readonly Lock _lock = new();
    private readonly ServerConfig _config = config;
    private WebApplication? _app;
    private IHostApplicationLifetime? _lifetime;
    private IBattleRoomManager? _battleRoomManager;
    private RoomCleanupLoop? _cleanupLoop;
    private bool _running;

    /// <summary>启动服务器：构建 Kestrel/SignalR 宿主并进入运行态。配置由构造注入。</summary>
    /// <returns>是否启动成功；失败时已清理已构建宿主。</returns>
    public bool Start() {
        lock (_lock) {
            if (_running) {
                _logger.LogWarning("服务器已在运行中");
                return true;
            }

            try {
                var builder = WebApplication.CreateBuilder();
                // 局域网游戏服务器，开发环境不启用 TLS
#pragma warning disable S5332
                builder.WebHost.UseUrls($"http://0.0.0.0:{_config.LobbyPort}");
#pragma warning restore S5332
                builder.Logging.ConfigureConsole();
                // Ctrl+C/SIGTERM 经宿主生命周期转为停止信号；.NET 10 中 ConsoleLifetime 已 internal，统一经扩展显式启用
                builder.Host.UseConsoleLifetime();
                builder.Services.AddServerHost(_config, loggerFactory);

                var app = builder.Build();
                // 先行登记宿主实例与生命周期：后续任一步失败可在 catch 完整释放
                _app = app;
                _lifetime = app.Lifetime;
                // 停止信号统一由宿主生命周期承载，业务清理挂 ApplicationStopping
                _lifetime.ApplicationStopping.Register(OnApplicationStopping);

                app.MapHub<LobbyHub>(HubPaths.Lobby);
                // 回放 HTTP 端点：列表与字节流下载，路由与凭证鉴权由回放服务端提供
                app.MapReplayEndpoints();
                app.Start();

                // 解析大厅应用契约校验 DI 装配完整性，依赖配置错误时构造函数抛异常进入 catch
                _ = app.Services.GetRequiredService<ILobbyApplication>();
                _battleRoomManager = app.Services.GetRequiredService<IBattleRoomManager>();

                _cleanupLoop = new RoomCleanupLoop(_battleRoomManager,
                    loggerFactory.CreateLogger<RoomCleanupLoop>());
                _cleanupLoop.Start();
                _running = true;

                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("服务器已启动，监听端口 {Port}", _config.LobbyPort);
                return true;
            }
            catch (Exception ex) {
                _cleanupLoop?.Stop(TimeSpan.FromSeconds(3));
                _cleanupLoop = null;
                _battleRoomManager?.StopAll();
                _battleRoomManager = null;
                _app?.DisposeAsync().AsTask().GetAwaiter().GetResult();
                _app = null;
                _lifetime = null;
                _running = false;
                _logger.LogError(ex, "服务器启动失败");
                return false;
            }
        }
    }

    /// <summary>
    /// 停止服务器：发出停止信号，触发宿主生命周期 ApplicationStopping 清理流程，
    /// 宿主收尾与进程退出由 <see cref="RunAsync"/> 完成。幂等。
    /// </summary>
    public void Stop() {
        IHostApplicationLifetime? lifetime;
        lock (_lock) {
            if (!_running)
                return;
            lifetime = _lifetime;
        }
        lifetime?.StopApplication();
    }

    /// <summary>
    /// 宿主停止阶段的业务清理：先停清理循环再停全部房间，保证房间线程退出后无并发协调。
    /// 由 ApplicationStopping 触发，与宿主停止流程同序；不触碰 _app，宿主收尾归 <see cref="RunAsync"/>。
    /// </summary>
    private void OnApplicationStopping() {
        lock (_lock) {
            if (!_running)
                return;

            try {
                _cleanupLoop?.Stop(TimeSpan.FromSeconds(3));
                _battleRoomManager?.StopAll();
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("服务器已停止");
            }
            catch (Exception ex) {
                _logger.LogError(ex, "服务器停止失败");
            }
            finally {
                _cleanupLoop = null;
                _battleRoomManager = null;
                _running = false;
            }
        }
    }

    /// <summary>
    /// 阻塞直至停止请求后完成宿主收尾；入口在 Start 后调用，替代手动 Thread.Sleep 保活。
    /// 停止信号来源：Ctrl+C/SIGTERM（宿主 ConsoleLifetime）或 <see cref="Stop"/>（父进程看护）。
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken = default) {
        WebApplication? app;
        lock (_lock) {
            if (!_running)
                return;
            app = _app;
        }

        if (app == null)
            return;

        try {
            // 等待停止信号（ApplicationStopping 触发后返回），再同步完成宿主停止
            await app.WaitForShutdownAsync(cancellationToken);
            await app.StopAsync(CancellationToken.None);
        }
        finally {
            await app.DisposeAsync();
            lock (_lock) {
                _app = null;
                _lifetime = null;
            }
        }
    }
}
