using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Core.Network.Dtos;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络客户端门面，持有大厅客户端和房间客户端两个持久实例。
/// 两个实例互斥连接：大厅连接时通过 _lobbyClient，加入房间后切换到 _roomClient。
/// 帧更新由 Godot 主线程 GameClientDriver 节点每帧驱动（Update 方法），
/// 不依赖后台线程（对齐 LiteEntitySystemUnityExample 的主线程驱动模式）。
/// 支持大厅→房间端口的重定向重连（由 GameClientService 内部桥接 OnRoomJoined 事件）。
/// 支持断线自动重连（通过缓存的 playerId + roomId 重新走大厅→房间流程）。
/// 连接连续性管理见 GameClientService.Connectivity。
/// </summary>
public sealed partial class GameClientService(ILoggerFactory loggerFactory) {
    private readonly ILogger<GameClientService> _logger = loggerFactory.CreateLogger<GameClientService>();

    // 连接状态机（单一事实源，见 ClientConnectionState）。
    // 取代散落的 _connected/_reconnecting 布尔与 _connectStartTimestamp 字段。
    private ClientConnectionState _state = ClientConnectionState.Idle;
    private long _stateStartTimestamp;

    // 当前活跃的客户端引用（由状态机维护）
    private IClientConnection? _activeClient;

    // SignalR 后台线程投递、需在主线程(Update)消费的动作队列。
    // LiteNetLib NetManager 非线程安全，所有对 RoomClient(NetManager) 的操作必须收敛到主线程，
    // 因此大厅回调只入队、不直接操作；由主线程每帧 Update 统一消费。
    private readonly System.Collections.Concurrent.ConcurrentQueue<System.Action> _mainThreadActions = new();

    // 加入房间时暂存的 roomId（房间端口连接成功后通过 OnRoomJoined 通知 UI）
    private string? _pendingJoinRoomId;
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

    /// <summary>是否已连接到服务器（已在大厅或房间）。</summary>
    public bool IsConnected => _state is ClientConnectionState.InLobby or ClientConnectionState.InRoom;

    /// <summary>
    /// 大厅客户端（持久实例，用于发送大厅请求和订阅事件）。
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

    /// <summary>
    /// 房间快照更新事件（主线程派发）。参数：房间 ID、完整快照。
    /// 面向显示层；底层 SignalR 回调经主线程队列转发，显示层无需自行 CallDeferred。
    /// </summary>
    public event Action<string, RoomSnapshot>? OnRoomSnapshotUpdated;

    /// <summary>成功加入房间事件（主线程派发）。参数：房间 ID。</summary>
    public event Action<string>? OnRoomJoined;

    /// <summary>成功创建房间事件（主线程派发）。参数：房间 ID。</summary>
    public event Action<string>? OnRoomCreated;

    /// <summary>房间列表接收事件（主线程派发）。参数：房间列表。</summary>
    public event Action<IReadOnlyList<RoomListing>>? OnRoomListReceived;

    /// <summary>获取指定房间最近一次快照（显示层进房初始化用）；不存在时返回 null。</summary>
    public RoomSnapshot? GetRoomSnapshot(string roomId) => LobbyClient.TryGetRoomSnapshot(roomId);

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
        if (IsConnected) {
            _logger.LogWarning("已连接到服务器");
            return;
        }

        try {
            Host = host;
            Port = port;

            WirePersistentEvents();

            LobbyClient.Connect(host, port);

            SetState(ClientConnectionState.ConnectingLobby);
            _activeClient = LobbyClient;
        }
        catch (Exception ex) {
            _activeClient = null;
            SetState(ClientConnectionState.Idle);
            _logger.LogError(ex, "连接失败");
            ConnectionChanged?.Invoke(host, port, false);
        }
    }

    /// <summary>
    /// 请求创建房间（通过大厅 SignalR 协议，含招募板配置）。
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
    /// 请求加入房间（通过大厅 SignalR 协议）。
    /// </summary>
    public void RequestJoinRoom(string roomId, string? roomPassword = null) {
        _cachedRoomId = roomId;
        _cachedRoomPassword = roomPassword;

        LobbyClient.RequestJoinRoom(roomId, PlayerName, PlayerId, roomPassword, _serverPassword);
    }

    /// <summary>请求在大厅准备阶段添加单位。</summary>
    public void RequestPrepareAddUnit(string roomId, string unitName, string camp) {
        LobbyClient.RequestPrepareAddUnit(roomId, unitName, camp);
    }

    /// <summary>请求在大厅准备阶段移除单位。</summary>
    public void RequestPrepareRemoveUnit(string roomId, string unitName, string camp) {
        LobbyClient.RequestPrepareRemoveUnit(roomId, unitName, camp);
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
        ClearRoomSessionCache();
        _pendingJoinRoomId = null;
        _pendingBattleRoomId = null;
        SetState(ClientConnectionState.Idle);

        _logger.LogInformation("连接已断开");
        ConnectionChanged?.Invoke(Host, Port, false);
    }

    /// <summary>
    /// 主动离开当前房间（准备阶段或战斗后）：显式清理会话缓存并复位状态，
    /// 防止后续房间意外断开时误触发对已离开房间的自动重连。
    /// 战斗退出由 MainScene.ExitBattle 调用。
    /// </summary>
    public void LeaveRoom() {
        try {
            RoomClient.Disconnect();
        }
        catch (Exception ex) {
            _logger.LogDebug(ex, "房间客户端断开异常");
        }

        _activeClient = null;
        ClearRoomSessionCache();
        _pendingJoinRoomId = null;
        _pendingBattleRoomId = null;
        SetState(LobbyClient.IsConnected ? ClientConnectionState.InLobby : ClientConnectionState.Idle);

        _logger.LogInformation("已离开房间");
    }

    /// <summary>清空断线重连所需的本房间会话缓存。</summary>
    private void ClearRoomSessionCache() {
        _cachedRoomId = null;
        _cachedRoomPort = 0;
        _cachedRoomPassword = null;
    }

    #region 持久事件绑定

    private bool _eventsWired;

    /// <summary>
    /// 将后台线程(SignalR 回调)产生的动作投递到主线程队列，由 Update 统一消费。
    /// 避免在回调线程直接操作 LiteNetLib NetManager 造成数据竞争。
    /// </summary>
    private void EnqueueMainThread(System.Action action) => _mainThreadActions.Enqueue(action);

    /// <summary>
    /// 绑定大厅与房间客户端的持久事件（幂等，仅执行一次）。
    /// 包括连接建立/断开、重定向、重连成功与失败等处理。
    /// </summary>
    private void WirePersistentEvents() {
        if (_eventsWired)
            return;
        _eventsWired = true;

        // 大厅客户端（SignalR 回调在后台线程触发，仅入队，不直接操作网络状态）
        LobbyClient.OnFullyConnected += () => EnqueueMainThread(() => {
            SetState(ClientConnectionState.InLobby);
            OnConnectionEstablished();
        });
        LobbyClient.OnFullyDisconnected += () => EnqueueMainThread(() => {
            // 大厅断开：若房间仍连（战斗中）则保持；否则视为完全断开
            if (_state is ClientConnectionState.InRoom or ClientConnectionState.ConnectingRoom)
                return;
            OnConnectionLost();
        });
        LobbyClient.OnRedirectToRoom += (roomId, roomPort) => EnqueueMainThread(() => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("收到重定向: {RoomId} → {Host}:{Port}", roomId, Host, roomPort);
            ReconnectToRoom(Host, roomPort, roomId);
        });
        LobbyClient.OnPrepareBattleRedirect += (roomId, roomPort) => EnqueueMainThread(() => {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("收到战斗重定向: {RoomId} → {Host}:{Port}", roomId, Host, roomPort);
            ReconnectToRoom(Host, roomPort, roomId, isBattleStart: true);
        });
        LobbyClient.OnReconnectFailed += (error) => EnqueueMainThread(() => {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("重连失败: {Error}", error);
            // 仅在重连状态中处理，失败后清缓存并复位，避免卡死在 Reconnecting
            if (_state == ClientConnectionState.Reconnecting) {
                ClearRoomSessionCache();
                SetState(LobbyClient.IsConnected ? ClientConnectionState.InLobby : ClientConnectionState.Idle);
                OnConnectionLost();
            }
        });
        // 房间快照及大厅中继事件：SignalR 后台回调 → 主线程派发（显示层无需自行 CallDeferred）
        LobbyClient.OnRoomSnapshotUpdated += (roomId, snapshot) => EnqueueMainThread(() => {
            OnRoomSnapshotUpdated?.Invoke(roomId, snapshot);
        });
        LobbyClient.OnRoomJoined += (roomId) => EnqueueMainThread(() => OnRoomJoined?.Invoke(roomId));
        LobbyClient.OnRoomCreated += (roomId) => EnqueueMainThread(() => OnRoomCreated?.Invoke(roomId));
        LobbyClient.OnRoomListReceived += (rooms) => EnqueueMainThread(() => OnRoomListReceived?.Invoke(rooms));

        // 房间客户端（LiteNetLib 回调在主线程 PollEvents 内触发）
        RoomClient.OnFullyConnected += () => {
            SetState(ClientConnectionState.InRoom);
            OnConnectionEstablished();

            // 战斗启动重定向：通知 UI 层 OnBattleStarted（不触发 OnRoomJoined）
            var battleRoomId = _pendingBattleRoomId;
            if (battleRoomId != null) {
                _pendingBattleRoomId = null;
                OnBattleStarted?.Invoke(battleRoomId);
                return;
            }

            // 桥接：从重定向进入房间后，触发统一 OnRoomJoined 事件
            var roomId = _pendingJoinRoomId;
            if (roomId != null) {
                _pendingJoinRoomId = null;
                OnRoomJoined?.Invoke(roomId);
            }
        };
        RoomClient.OnFullyDisconnected += () => {
            // 主动离开（LeaveRoom/Disconnect 用 _netClient.Stop 不触发此事件），此处为意外断开
            if (_state is not (ClientConnectionState.InRoom or ClientConnectionState.ConnectingRoom))
                return;

            // 房间意外断开：尝试自动重连；无缓存房间则按大厅状态复位
            if (!string.IsNullOrEmpty(_cachedRoomId)) {
                _logger.LogInformation("房间连接意外断开，尝试自动重连...");
                AttemptReconnectToRoom();
            } else {
                SetState(LobbyClient.IsConnected ? ClientConnectionState.InLobby : ClientConnectionState.Idle);
                OnConnectionLost();
            }
        };
    }

    #endregion
}
