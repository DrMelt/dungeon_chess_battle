using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络客户端门面，持有大厅客户端和房间客户端两个持久实例。
/// 两个实例互斥连接：大厅连接时通过 _lobbyClient，加入房间后切换到 _roomClient。
/// 帧更新由 Godot 主线程 GameClientDriver 节点每帧驱动（Update 方法），
/// 不依赖后台线程（对齐 LiteEntitySystemUnityExample 的主线程驱动模式）。
/// 支持本地模式回退。
/// 支持大厅→房间端口的重定向重连（由 GameClientService 内部桥接 OnRoomJoined 事件）。
/// 支持断线自动重连（通过缓存的 playerId + roomId 重新走大厅→房间流程）。
/// 连接连续性管理见 GameClientService.Connectivity。
/// </summary>
public sealed partial class GameClientService(ILoggerFactory loggerFactory) {
    private readonly ILogger<GameClientService> _logger = loggerFactory.CreateLogger<GameClientService>();
    private GameLogicService? _localService;
    private volatile bool _connected;

    // 两个持久客户端实例

    // 当前活跃的客户端引用
    private NetworkClientBase? _activeClient;

    // 重连中标志：防止 OnConnectionLost 在断连-重连窗口误停更新循环
    private volatile bool _reconnecting;

    // 加入房间时暂存的 roomId（房间端口连接成功后通过 OnRoomJoined 通知 UI）
    private string? _pendingJoinRoomId;
    private long _connectStartTimestamp;
    private const double ConnectTimeoutSeconds = 10.0;

    /// <summary>默认大厅端口。</summary>
    public const int DefaultPort = 10170;

    // 身份与会话缓存

    /// <summary>服务器密码（null 表示无密码开发模式）</summary>
    private string? _serverPassword;

    /// <summary>当前所在的房间 ID（用于断线重连）</summary>
    private string? _cachedRoomId;

    /// <summary>当前房间端口（用于断线重连）</summary>
    private int _cachedRoomPort;

    /// <summary>当前房间密码（用于重连验证）</summary>
    private string? _cachedRoomPassword;

    // 公开属性

    /// <summary>是否已连接到服务器（大厅或房间）。</summary>
    public bool IsConnected => _connected;

    /// <summary>
    /// 当前活跃的客户端接口（大厅客户端或房间客户端或本地服务）。
    /// </summary>
    public IClientBattleService? Client =>
        _connected ? RoomClient : _localService;

    /// <summary>
    /// 大厅客户端（持久实例，用于发送 JSON 命令和订阅事件）。
    /// </summary>
    public LobbyClient LobbyClient { get; } = new(loggerFactory.CreateLogger<LobbyClient>());

    /// <summary>
    /// 房间客户端（持久实例，用于 LES Entity 同步）。
    /// </summary>
    public RoomBattleClient RoomClient { get; } = new(loggerFactory.CreateLogger<RoomBattleClient>());

    /// <summary>服务器主机地址。</summary>
    public string Host { get; private set; } = "";

    /// <summary>当前监听端口。</summary>
    public int Port {
        get; private set;
    }

    /// <summary>客户端持久玩家 ID。</summary>
    public string PlayerId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>玩家显示名。</summary>
    public string PlayerName { get; private set; } = "Player";

    /// <summary>连接状态变化事件。参数：主机、端口、是否已连接。</summary>
    public event Action<string, int, bool>? ConnectionChanged;

    /// <summary>战斗启动事件（网络模式：房间端口连接成功后触发）。参数：房间 ID。</summary>
    public event Action<string>? OnBattleStarted;

    // 配置方法（在 Connect 前调用）

    /// <summary>
    /// 设置玩家身份信息。在 Connect 前调用。
    /// </summary>
    public void Configure(string playerName, string? serverPassword = null) {
        PlayerName = playerName;
        _serverPassword = string.IsNullOrEmpty(serverPassword) ? null : serverPassword;
    }

    // 连接管理

    /// <summary>
    /// 连接服务器大厅（默认大厅端口）。
    /// </summary>
    /// <param name="host">服务器主机地址。</param>
    /// <param name="port">大厅端口。</param>
    public void Connect(string host, int port = DefaultPort) {
        if (_connected) {
            _logger.LogWarning("已连接到服务器");
            return;
        }

        try {
            Host = host;
            Port = port;

            WirePersistentEvents();

            // 使用服务器密码作为 ConnectionKey（无密码时使用默认值）
            string connectionKey = _serverPassword ?? NetworkClientBase.ConnectionKey;
            LobbyClient.Connect(host, port, connectionKey);

            _activeClient = LobbyClient;
            _connectStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
        }
        catch (Exception ex) {
            _activeClient = null;
            _connected = false;
            _logger.LogError(ex, "连接失败");
            ConnectionChanged?.Invoke(host, port, false);
        }
    }

    /// <summary>
    /// 请求创建房间（通过大厅 JSON 协议，含招募板配置）。
    /// </summary>
    public void RequestCreateRoom(string roomId, string? roomPassword = null, GameRoom? config = null) {
        _cachedRoomId = roomId;
        _cachedRoomPassword = roomPassword;

        LobbyClient.RequestCreateRoom(roomId, PlayerName, PlayerId,
            roomPassword, config, _serverPassword);
    }

    /// <summary>
    /// 请求房间列表（招募板）。
    /// </summary>
    public void RequestListRooms() {
        LobbyClient.RequestListRooms();
    }

    /// <summary>
    /// 请求加入房间（通过大厅 JSON 协议）。
    /// </summary>
    public void RequestJoinRoom(string roomId, string? roomPassword = null) {
        _cachedRoomId = roomId;
        _cachedRoomPassword = roomPassword;

        var msg = MessageWriter.WriteRoomRequestFull(
            MessageType.JoinRoom, roomId, PlayerName,
            roomPassword, PlayerId, _serverPassword);
        LobbyClient.SendCommand(msg);
    }

    /// <summary>
    /// 断开全部连接并清理活动客户端状态。
    /// </summary>
    public void Disconnect() {
        try {
            LobbyClient.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "大厅客户端断开异常");
        }
        try {
            RoomClient.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "房间客户端断开异常");
        }

        _activeClient = null;
        _connected = false;
        _cachedRoomId = null;

        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(Host, Port, false);
    }

    /// <summary>
    /// 初始化本地模式（单人离线），使用 GameLogicService 作为服务端与客户端。
    /// </summary>
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

    /// <summary>
    /// 绑定大厅与房间客户端的持久事件（幂等，仅执行一次）。
    /// 包括连接建立/断开、重定向、重连成功与失败等处理。
    /// </summary>
    private void WirePersistentEvents() {
        if (_eventsWired)
            return;
        _eventsWired = true;

        // 大厅客户端
        LobbyClient.OnFullyConnected += () => {
            _connected = true;
            OnConnectionEstablished();
        };
        LobbyClient.OnFullyDisconnected += () => {
            _connected = RoomClient.IsConnected;
            OnConnectionLost();
        };
        LobbyClient.OnRedirectToRoom += (roomId, roomPort) => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("收到重定向: {RoomId} → {Host}:{Port}", roomId, Host, roomPort);
            ReconnectToRoom(Host, roomPort, roomId);
        };
        LobbyClient.OnPrepareBattleRedirect += (roomId, roomPort) => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("收到战斗重定向: {RoomId} → {Host}:{Port}", roomId, Host, roomPort);
            _cachedRoomPort = roomPort;
            ReconnectToRoom(Host, roomPort, roomId, isBattleStart: true);
        };
        LobbyClient.OnReconnectFailed += (error) => {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("重连失败: {Error}", error);
            _reconnecting = false;
            // 通知 UI 层重连失败
            OnConnectionLost();
        };

        // 房间客户端
        RoomClient.OnFullyConnected += () => {
            _connected = true;
            _reconnecting = false;
            OnConnectionEstablished();

            // 战斗启动重定向：通知 UI 层 OnBattleStarted（不触发 OnRoomJoined）
            var battleRoomId = _pendingBattleRoomId;
            if (battleRoomId != null) {
                _pendingBattleRoomId = null;
                OnBattleStarted?.Invoke(battleRoomId);
                return;
            }

            // 桥接：从重定向进入房间后，通知 UI 层 OnRoomJoined
            var roomId = _pendingJoinRoomId;
            if (roomId != null) {
                _pendingJoinRoomId = null;
                LobbyClient.TriggerRoomJoined(roomId);
            }
        };
        RoomClient.OnFullyDisconnected += () => {
            _connected = LobbyClient.IsConnected;

            // 如果房间连接断开且不在重连中，尝试自动重连
            if (!_reconnecting && !string.IsNullOrEmpty(_cachedRoomId)) {
                _logger.LogInformation("房间连接意外断开，尝试自动重连...");
                AttemptReconnectToRoom();
            }
            else {
                OnConnectionLost();
            }
        };
        RoomClient.OnReconnectSucceeded += (roomId) => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("重连成功: {RoomId}", roomId);
        };
    }

    #endregion
}
