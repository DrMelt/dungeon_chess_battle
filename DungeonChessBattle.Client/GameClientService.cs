using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络客户端门面，包装 NetworkBattleClient 实例。
/// 通过内部后台线程驱动帧更新，不依赖 Godot 节点生命周期。
/// 支持本地模式回退。
/// </summary>
public sealed class GameClientService(ILogger<GameClientService> logger) {
    private readonly ILogger<GameClientService> _logger = logger;
    private NetworkBattleClient? _client;
    private GameLogicService? _localService;
    private Thread? _updateThread;
    private volatile bool _running;
    private volatile bool _connected;

    private string _host = "";
    private int _port;
    private const double TickInterval = 0.05; // 20 Hz

    public const int DefaultPort = 10170;

    public bool IsConnected => _connected;

    public IClientBattleService? Client =>
        _connected ? _client : _localService;

    public string Host => _host;

    public int Port => _port;

    public event Action<string, int, bool>? ConnectionChanged;

    public void Connect(string host, int port = DefaultPort) {
        if (_connected) {
            _logger.LogWarning("已连接到服务器");
            return;
        }

        try {
            _host = host;
            _port = port;
            _client = new NetworkBattleClient();
            _client.Connect(host, port);
            _connected = true;

            StartUpdateLoop();

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("已连接到 {Host}:{Port}", host, port);
            ConnectionChanged?.Invoke(host, port, true);
        }
        catch (Exception ex) {
            _client = null;
            _connected = false;
            _logger.LogError(ex, "连接失败");
            ConnectionChanged?.Invoke(host, port, false);
        }
    }

    public void Disconnect() {
        StopUpdateLoop();

        try {
            _client?.Disconnect();
        }
        catch { }

        _client = null;
        _connected = false;

        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(_host, _port, false);
    }

    public void InitLocalMode() {
        if (_connected) {
            _logger.LogInformation("已在网络模式，跳过本地初始化");
            return;
        }

        _localService ??= new GameLogicService();
        _logger.LogInformation("本地模式已初始化");
    }

    #region Internal Update Loop

    private void StartUpdateLoop() {
        if (_running)
            return;

        _running = true;
        _updateThread = new Thread(RunUpdate) {
            Name = "GameClient-Update",
            IsBackground = true,
        };
        _updateThread.Start();
    }

    private void StopUpdateLoop() {
        _running = false;
        _updateThread?.Join(TimeSpan.FromSeconds(3));
        _updateThread = null;
    }

    private void RunUpdate() {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        double lastTick = 0;

        while (_running) {
            double now = watch.Elapsed.TotalSeconds;
            double delta = now - lastTick;

            if (delta >= TickInterval) {
                lastTick = now;
                try {
                    _client?.Update((float)delta);
                }
                catch { }
            }

            Thread.Sleep(1);
        }
    }

    #endregion
}
