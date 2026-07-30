using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server;

/// <summary>
/// 游戏服务器门面，包装 GameServer 实例。
/// 提供 Start/Stop 操作和状态事件通知。
/// GameServer 内部已有独立驱动线程，本类不创建额外线程。
/// </summary>
public sealed class GameServerService(ILogger<GameServerService> logger) {
    private readonly ILogger<GameServerService> _logger = logger;
    private GameServer? _server;
    private int _port;

    public const int DefaultPort = 10170;

    public bool IsRunning => _server?.IsRunning ?? false;
    public int Port => _port;

    public event Action<bool, int>? StatusChanged;

    public void Start(int port = DefaultPort) {
        if (IsRunning) {
            _logger.LogWarning("服务器已在运行中");
            return;
        }

        try {
            _port = port;
            _server = new GameServer();
            _server.StartAsync(port);

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("服务器已启动，监听端口 {Port}", port);
            StatusChanged?.Invoke(true, port);
        }
        catch (Exception ex) {
            _server = null;
            _port = 0;
            _logger.LogError(ex, "服务器启动失败");
            StatusChanged?.Invoke(false, 0);
        }
    }

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
            _port = 0;
            StatusChanged?.Invoke(false, 0);
        }
    }
}
