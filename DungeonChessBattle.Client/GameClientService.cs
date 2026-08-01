using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络客户端门面，持有大厅客户端和房间客户端两个持久实例。
/// 两个实例互斥连接：大厅连接时通过 _lobbyClient，加入房间后切换到 _roomClient。
/// 通过内部后台线程驱动帧更新，不依赖 Godot 节点生命周期。
/// 支持本地模式回退。
/// 支持大厅→房间端口的重定向重连（由 GameClientService 内部桥接 OnRoomJoined 事件）。
/// </summary>
public sealed class GameClientService(ILoggerFactory loggerFactory) {
    private readonly ILogger<GameClientService> _logger = loggerFactory.CreateLogger<GameClientService>();
    private GameLogicService? _localService;
    private Thread? _updateThread;
    private volatile bool _running;
    private volatile bool _connected;

    // 两个持久客户端实例
    private readonly LobbyClient _lobbyClient = new(loggerFactory.CreateLogger<LobbyClient>());
    private readonly RoomBattleClient _roomClient = new(loggerFactory.CreateLogger<RoomBattleClient>());

    // 当前活跃的客户端引用
    private NetworkClientBase? _activeClient;

    // 重连中标志：防止 OnConnectionLost 在断连-重连窗口误停更新循环
    private volatile bool _reconnecting;

    // 加入房间时暂存的 roomId（房间端口连接成功后通过 OnRoomJoined 通知 UI）
    private string? _pendingJoinRoomId;

    private string _host = "";
    private int _port;
    private long _connectStartTimestamp;
    private const double TickInterval = 0.05; // 20 Hz
    private const double ConnectTimeoutSeconds = 10.0;

    public const int DefaultPort = 10170;

    public bool IsConnected => _connected;

    /// <summary>
    /// 当前活跃的客户端接口（大厅客户端或房间客户端或本地服务）。
    /// </summary>
    public IClientBattleService? Client =>
        _connected ? _roomClient : _localService;

    /// <summary>
    /// 大厅客户端（持久实例，用于发送 JSON 命令和订阅事件）。
    /// </summary>
    public LobbyClient LobbyClient => _lobbyClient;

    /// <summary>
    /// 房间客户端（持久实例，用于 LES Entity 同步）。
    /// </summary>
    public RoomBattleClient RoomClient => _roomClient;

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

            WirePersistentEvents();

            _connectStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _lobbyClient.Connect(host, port);
            _activeClient = _lobbyClient;

            StartUpdateLoop();
        }
        catch (Exception ex) {
            _activeClient = null;
            _connected = false;
            _logger.LogError(ex, "连接失败");
            ConnectionChanged?.Invoke(host, port, false);
        }
    }

    /// <summary>
    /// 重连到房间端口。大厅连接保持不断开。
    /// 由大厅重定向触发，用于切换到物理隔离的房间 SEM。
    /// </summary>
    private void ReconnectToRoom(string host, int roomPort, string roomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("重连至房间端口: {Host}:{Port}, RoomId={RoomId}", host, roomPort, roomId);

        _reconnecting = true;
        try {
            _host = host;
            _port = roomPort;
            _connected = false;
            _pendingJoinRoomId = roomId;

            _roomClient.Reconnect(host, roomPort);
            _activeClient = _roomClient;

            _connectStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        catch (Exception ex) {
            _activeClient = null;
            _connected = false;
            _logger.LogError(ex, "重连至房间端口失败");
            ConnectionChanged?.Invoke(host, roomPort, false);
        }
        finally {
            _reconnecting = false;
        }
    }

    private void OnConnectionEstablished() {
        _connectStartTimestamp = 0;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("已连接到 {Host}:{Port}", _host, _port);
        ConnectionChanged?.Invoke(_host, _port, true);
    }

    private void OnConnectionLost() {
        if (_lobbyClient.IsConnected || _roomClient.IsConnected) {
            _connected = _lobbyClient.IsConnected || _roomClient.IsConnected;
            return;
        }

        _connected = false;

        if (_reconnecting) {
            return;
        }

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
            _lobbyClient.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "大厅客户端断开异常");
        }
        try {
            _roomClient.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "房间客户端断开异常");
        }

        _activeClient = null;
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

    #region 持久事件绑定

    private bool _eventsWired;

    private void WirePersistentEvents() {
        if (_eventsWired)
            return;
        _eventsWired = true;

        // ── 大厅客户端 ──
        _lobbyClient.OnFullyConnected += () => {
            _connected = true;
            OnConnectionEstablished();
        };
        _lobbyClient.OnFullyDisconnected += () => {
            _connected = _roomClient.IsConnected;
            OnConnectionLost();
        };
        _lobbyClient.OnRedirectToRoom += (roomId, roomPort) => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("收到重定向: {RoomId} → {Host}:{Port}", roomId, _host, roomPort);
            ReconnectToRoom(_host, roomPort, roomId);
        };

        // ── 房间客户端 ──
        _roomClient.OnFullyConnected += () => {
            _connected = true;
            OnConnectionEstablished();

            // 桥接：从重定向进入房间后，通知 UI 层 OnRoomJoined
            var roomId = _pendingJoinRoomId;
            if (roomId != null) {
                _pendingJoinRoomId = null;
                // 通过 _lobbyClient 的 TriggerRoomJoined 通知 UI
                // （UI 已经订阅了 _lobbyClient.OnRoomJoined）
                _lobbyClient.TriggerRoomJoined(roomId);
            }
        };
        _roomClient.OnFullyDisconnected += () => {
            _connected = _lobbyClient.IsConnected;
            OnConnectionLost();
        };
    }

    #endregion

    #region Update Loop

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
            _activeClient?.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "断开连接异常");
        }
        _activeClient = null;

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
                    _lobbyClient.Update((float)delta);
                    _roomClient.Update((float)delta);
                }
                catch (Exception ex) {
                    _logger.LogWarning(ex, "客户端更新异常");
                }

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
