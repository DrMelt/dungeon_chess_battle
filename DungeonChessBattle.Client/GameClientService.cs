using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络客户端门面，包装 NetworkBattleClient 实例。
/// 通过内部后台线程驱动帧更新，不依赖 Godot 节点生命周期。
/// 支持本地模式回退。
/// </summary>
public sealed class GameClientService(ILogger<GameClientService> logger, ILoggerFactory loggerFactory) {
    private readonly ILogger<GameClientService> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private NetworkBattleClient? _client;
    private GameLogicService? _localService;
    private Thread? _updateThread;
    private volatile bool _running;
    private volatile bool _connected;

    private string _host = "";
    private int _port;
    private long _connectStartTimestamp;
    private const double TickInterval = 0.05; // 20 Hz
    private const double ConnectTimeoutSeconds = 10.0;

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
            _client = new NetworkBattleClient(_loggerFactory.CreateLogger<NetworkBattleClient>());

            // 连接状态改为由底层回调驱动，不再立即设为 true
            _client.OnFullyConnected += () => {
                _connected = true;
                OnConnectionEstablished();
            };
            _client.OnFullyDisconnected += () => {
                _connected = false;
                OnConnectionLost();
            };

            _connectStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _client.Connect(host, port);

            // 必须立即启动更新循环以驱动 PollEvents（否则 OnPeerConnected 永不触发）
            StartUpdateLoop();
        }
        catch (Exception ex) {
            _client = null;
            _connected = false;
            _logger.LogError(ex, "连接失败");
            ConnectionChanged?.Invoke(host, port, false);
        }
    }

    private void OnConnectionEstablished() {
        _connectStartTimestamp = 0; // 清除超时计时
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("已连接到 {Host}:{Port}", _host, _port);
        ConnectionChanged?.Invoke(_host, _port, true);
    }

    private void OnConnectionLost() {
        // OnPeerDisconnected 在 RunUpdate 线程中触发，不能 Join 自身
        _running = false;
        if (_updateThread != null && Thread.CurrentThread != _updateThread) {
            _updateThread.Join(TimeSpan.FromSeconds(3));
            _updateThread = null;
        }
        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(_host, _port, false);
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
        // 避免在 RunUpdate 线程中 Join 自身（如断线回调触发的 StopUpdateLoop）
        if (_updateThread != null && Thread.CurrentThread != _updateThread) {
            _updateThread.Join(TimeSpan.FromSeconds(3));
        }
        _updateThread = null;
    }

    private void HandleConnectionTimeout() {
        _logger.LogWarning("连接超时 ({Host}:{Port})", _host, _port);
        _connectStartTimestamp = 0;
        _running = false;
        _updateThread = null;

        try {
            _client?.Disconnect();
        }
        catch { }
        _client = null;

        ConnectionChanged?.Invoke(_host, _port, false);
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

                // 连接超时检查
                if (!_connected && _connectStartTimestamp != 0) {
                    double elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(_connectStartTimestamp).TotalSeconds;
                    if (elapsed > ConnectTimeoutSeconds) {
                        HandleConnectionTimeout();
                        return;
                    }
                }
            }

            Thread.Sleep(1);
        }
    }

    #endregion
}
