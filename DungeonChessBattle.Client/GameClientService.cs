using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络客户端门面，包装 NetworkBattleClient 实例。
/// 通过内部后台线程驱动帧更新，不依赖 Godot 节点生命周期。
/// 支持本地模式回退。
/// 支持大厅→房间端口的重定向重连。
/// </summary>
public sealed class GameClientService(ILogger<GameClientService> logger, ILoggerFactory loggerFactory) {
    private readonly ILogger<GameClientService> _logger = logger;
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private NetworkBattleClient? _client;
    private GameLogicService? _localService;
    private Thread? _updateThread;
    private volatile bool _running;
    private volatile bool _connected;

    // 重连中标志：防止 OnConnectionLost 在断连-重连窗口误停更新循环
    private volatile bool _reconnecting;

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
            _client = CreateClient();
            WireClientEvents(_client);

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

    /// <summary>
    /// 断开当前连接并重新连接到房间端口。
    /// 由大厅重定向触发，用于切换到物理隔离的房间 SEM。
    /// 此方法在 RunUpdate 线程内被调用，故不启动新线程——就地热替换 _client。
    /// </summary>
    private void ReconnectToRoom(string host, int roomPort) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("重连至房间端口: {Host}:{Port}", host, roomPort);

        _reconnecting = true;
        try {
            // 安全拆卸旧客户端：先取消事件订阅，再断开
            var oldClient = _client;
            if (oldClient != null) {
                UnwireClientEvents(oldClient);
                try {
                    oldClient.Disconnect();
                }
                catch (Exception ex) { _logger.LogDebug(ex, "旧客户端断开异常"); }
            }

            _host = host;
            _port = roomPort;
            _connected = false;

            var newClient = CreateClient();
            WireClientEvents(newClient);
            _client = newClient;

            _connectStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            newClient.Connect(host, roomPort);
        }
        catch (Exception ex) {
            _client = null;
            _connected = false;
            _logger.LogError(ex, "重连至房间端口失败");
            ConnectionChanged?.Invoke(host, roomPort, false);
        }
        finally {
            _reconnecting = false;
        }
    }

    private void OnConnectionEstablished() {
        _connectStartTimestamp = 0; // 清除超时计时
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("已连接到 {Host}:{Port}", _host, _port);
        ConnectionChanged?.Invoke(_host, _port, true);
    }

    private void OnConnectionLost() {
        _connected = false;

        if (_reconnecting) {
            // 重连窗口内：只标记断连，不停止更新循环
            return;
        }

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
            var oldClient = _client;
            if (oldClient != null) {
                UnwireClientEvents(oldClient);
                oldClient.Disconnect();
            }
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "断开连接异常");
        }

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

    #region Internal Helpers

    // 存储委托引用以支持取消订阅
    private Action? _onFullyConnectedHandler;
    private Action? _onFullyDisconnectedHandler;
    private Action<string, int>? _onRedirectToRoomHandler;

    /// <summary>创建新的 NetworkBattleClient 实例（不含事件订阅）。</summary>
    private NetworkBattleClient CreateClient() =>
        new(_loggerFactory.CreateLogger<NetworkBattleClient>());

    /// <summary>为客户端实例订阅事件回调。</summary>
    private void WireClientEvents(NetworkBattleClient client) {
        _onFullyConnectedHandler = () => {
            _connected = true;
            OnConnectionEstablished();
        };
        _onFullyDisconnectedHandler = () => {
            _connected = false;
            OnConnectionLost();
        };
        _onRedirectToRoomHandler = (roomId, roomPort) => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("收到重定向: {RoomId} → {Host}:{Port}", roomId, _host, roomPort);
            ReconnectToRoom(_host, roomPort);
        };

        client.OnFullyConnected += _onFullyConnectedHandler;
        client.OnFullyDisconnected += _onFullyDisconnectedHandler;
        client.OnRedirectToRoom += _onRedirectToRoomHandler;
    }

    /// <summary>取消客户端事件订阅，释放旧 client。</summary>
    private void UnwireClientEvents(NetworkBattleClient client) {
        if (_onFullyConnectedHandler != null)
            client.OnFullyConnected -= _onFullyConnectedHandler;
        if (_onFullyDisconnectedHandler != null)
            client.OnFullyDisconnected -= _onFullyDisconnectedHandler;
        if (_onRedirectToRoomHandler != null)
            client.OnRedirectToRoom -= _onRedirectToRoomHandler;

        _onFullyConnectedHandler = null;
        _onFullyDisconnectedHandler = null;
        _onRedirectToRoomHandler = null;
    }

    #endregion

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
        catch (Exception ex) {
            _logger.LogDebug(ex, "断开连接异常");
        }
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
                catch (Exception ex) {
                    _logger.LogWarning(ex, "客户端更新异常");
                }

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
