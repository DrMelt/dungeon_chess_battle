using DungeonChessBattle.Server.Settings;
using DungeonChessBattle.Server.Stores;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务器门面，包装 GameServer 实例。
/// 提供 Start/Stop 操作和状态事件通知。
/// GameServer 内部已有独立驱动线程，本类不创建额外线程。
/// 服务器配置由 <see cref="ServerConfig"/> 唯一来源注入。
/// </summary>
public sealed class GameServerHost(ILogger<GameServerHost> logger, ILoggerFactory loggerFactory) {
    private readonly ILogger<GameServerHost> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private GameServer? _server;
    private InMemoryGameStateStore? _stateStore;

    /// <summary>默认大厅端口。</summary>
    public const int DefaultPort = ServerConfig.DefaultPort;

    /// <summary>服务器是否正在运行。</summary>
    public bool IsRunning => _server?.IsRunning ?? false;

    /// <summary>当前监听端口。</summary>
    public int Port {
        get; private set;
    }

    /// <summary>服务器状态变化事件。参数：是否运行、端口。</summary>
    public event Action<bool, int>? StatusChanged;

    /// <summary>
    /// 启动服务器。
    /// </summary>
    /// <param name="port">大厅监听端口。</param>
    /// <param name="serverPassword">服务器访问密码；为空表示不启用。</param>
    public void Start(int port = DefaultPort, string? serverPassword = null) {
        if (IsRunning) {
            _logger.LogWarning("服务器已在运行中");
            return;
        }

        try {
            var config = ServerConfig.FromEnvironment(serverPassword) with {
                LobbyPort = port
            };
            Port = config.LobbyPort;
            _stateStore = new InMemoryGameStateStore(_loggerFactory);
            _server = new GameServer(_loggerFactory, config, _stateStore);
            _server.StartAsync();

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("服务器已启动，监听端口 {Port}", port);
            StatusChanged?.Invoke(true, port);
        }
        catch (Exception ex) {
            _server = null;
            _stateStore?.Dispose();
            _stateStore = null;
            Port = 0;
            _logger.LogError(ex, "服务器启动失败");
            StatusChanged?.Invoke(false, 0);
        }
    }

    /// <summary>
    /// 停止服务器并触发状态通知。
    /// </summary>
    public void Stop() {
        if (!IsRunning || _server is null) {
            _logger.LogWarning("服务器未在运行");
            return;
        }

        try {
            _server.Stop();
            _logger.LogInformation("服务器已停止");
        }
        catch (Exception ex) {
            _logger.LogError(ex, "服务器停止失败");
        }
        finally {
            _server = null;
            _stateStore?.Dispose();
            _stateStore = null;
            Port = 0;
            StatusChanged?.Invoke(false, 0);
        }
    }
}
