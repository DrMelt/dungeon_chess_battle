using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 游戏大厅主控脚本，负责房间列表展示（招募板）、创建/加入房间等操作。
/// 服务从 ServiceLocator 获取，不再由外部注入。
/// 房间列表（招募板）UI 处理见 GameLobby.RoomList。
/// </summary>
public partial class GameLobby : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<GameLobby> _logger = ServiceLocator.GetLogger<GameLobby>();

    #region Signals

    /// <summary>
    /// 战斗开始（从准备界面发起的请求）。
    /// </summary>
    [Signal]
    public delegate void BattleStartedEventHandler(string roomId);

    #endregion

    #region References

    /// <summary>房间准备界面引用。</summary>
    [Export]
    private RoomPreparation? _roomPreparation;

    /// <summary>
    /// 公开服务实例，供外部组件获取。
    /// </summary>
    public GameLobbyInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 当前客户端服务。从 ServiceLocator 获取。
    /// </summary>
    private static IClientBattleService? ClientService => ServiceLocator.ClientService.Client;

    #endregion

    #region State

    /// <summary>当前选中的房间 ID。</summary>
    private string? _selectedRoomId;
    /// <summary>房间 ID 到卡片节点的缓存。</summary>
    private readonly Dictionary<string, RoomInfo> _roomInfoCache = [];
    /// <summary>当前筛选的房间类别。</summary>
    private RoomCategory _selectedCategory = RoomCategory.Casual;
    /// <summary>缓存的服务端房间列表。</summary>
    private List<RoomListing>? _pendingRoomListings;
    /// <summary>缓存创建房间时输入的房间名。</summary>
    private string? _cachedCreateRoomId;
    /// <summary>当前选中房间的配置。</summary>
    private GameRoom? _selectedRoomConfig;

    #endregion

    /// <summary>
    /// 节点就绪：获取引用集合、连接按钮与准备界面信号，并订阅大厅客户端事件。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<GameLobbyInterRefs>("GameLobbyInterRefs");
        if (InterRefs is null) {
            GD.PrintErr("[GameLobby] GameLobbyInterRefs node not found.");
            return;
        }

        // 连接 RoomPreparation 信号
        if (_roomPreparation is not null)
            _roomPreparation.BattleStartRequested += StartBattle;
        else
            GD.PrintErr("[GameLobby] RoomPreparation reference is not assigned. Room preparation will be unavailable.");

        // 连接按钮信号
        InterRefs?.CreateButton?.Pressed += OnCreateRoom;
        InterRefs?.RefreshButton?.Pressed += OnRefreshRooms;
        InterRefs?.BackButton?.Pressed += GoBack;
        var joinBtn = InterRefs?.JoinButton;
        if (joinBtn is not null) {
            joinBtn.Pressed += OnJoinRoom;
            joinBtn.Disabled = true;
        }

        // 持久订阅大厅客户端事件
        ServiceLocator.ClientService.LobbyClient.OnRoomJoined += OnRoomJoinedHandler;
        ServiceLocator.ClientService.LobbyClient.OnRoomCreated += OnRoomCreatedHandler;
        SubscribeRoomListEvent();

        // 网络模式：服务端确认战斗启动（房间端口连接成功后）→ 桥接 BattleStarted 信号
        ServiceLocator.ClientService.OnBattleStarted += OnNetworkBattleStarted;

        _logger.LogInformation("GameLobby ready");


    }

    /// <summary>
    /// 面板每次被 NavigateTo 打开时触发一次延迟刷新，
    /// 给 Entity 同步留出时间（网络模式下 OnPeerConnected → Entity 构造需要数个 Tick）。
    /// </summary>
    protected override void OnPanelOpened() {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("GameLobby opened, connected={IsConnected}", ServiceLocator.ClientService.IsConnected);
        // 防御性初始化：确保本地服务已就绪（防止面板在 InitLocalMode 之前被打开）
        if (!ServiceLocator.ClientService.IsConnected && ServiceLocator.ClientService.Client == null) {
            ServiceLocator.ClientService.InitLocalMode();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("InitLocalMode called from OnPanelOpened (defensive)");
        }
        CallDeferred(nameof(OnRefreshRooms));
    }

    #region Button Handlers

    /// <summary>
    /// 点击创建房间按钮：校验房间名后，网络模式发送创建请求，本地模式直接创建。
    /// </summary>
    private void OnCreateRoom() {
        string roomName = InterRefs?.RoomNameInput?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(roomName)) {
            _logger.LogWarning("创建房间失败: 房间名为空");
            return;
        }

        InterRefs?.RoomNameInput?.Clear();

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：发送 create_room，等待 OnRoomCreated 回调后导航
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("请求创建房间(网络): {RoomName}", roomName);
            _cachedCreateRoomId = roomName;
            ServiceLocator.ClientService.RequestCreateRoom(roomName);
        }
        else {
            // 本地模式：通过接口创建
            var clientService = ClientService;
            if (clientService != null) {
                clientService.CreateRoom(roomName);
                if (_logger.IsEnabled(LogLevel.Information))
                    _logger.LogInformation("本地房间创建成功: {RoomName}", roomName);
                if (_roomPreparation != null) {
                    var localConfig = clientService.GetRoom(roomName);
                    _roomPreparation.EnterRoom(roomName, localConfig, isHost: true);
                    NavigateTo(_roomPreparation);
                }
            }
            else {
                _logger.LogError("创建房间失败: 无可用服务");
            }
        }
    }

    /// <summary>
    /// 网络模式：服务端确认战斗启动后由 GameClientService 触发，
    /// 桥接为 BattleStarted 信号，复用本地模式进入战斗的同一事件链。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    private void OnNetworkBattleStarted(string roomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("网络战斗启动: {RoomId}", roomId);
        EmitSignal(SignalName.BattleStarted, roomId);
    }

    /// <summary>
    /// 公开方法：开始战斗（本地模式）。由 RoomPreparation 调用。
    /// 网络模式下 RoomPreparation 直接通过 LobbyClient.RequestPrepareStartBattle 发送。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    public void StartBattle(string roomId) {
        var clientService = ClientService;
        if (clientService == null) {
            _logger.LogError("启动战斗失败: 房间 {RoomId} 无可用服务", roomId);
            return;
        }

        // 本地模式通过接口调用
        clientService.RequestStartBattle(roomId);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("本地战斗启动: {RoomId}", roomId);

        EmitSignal(SignalName.BattleStarted, roomId);
    }

    /// <summary>
    /// 点击加入按钮：校验已选中房间后发送加入请求。
    /// </summary>
    private void OnJoinRoom() {
        if (string.IsNullOrEmpty(_selectedRoomId)) {
            _logger.LogWarning("加入房间失败: 未选中房间");
            return;
        }

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：发送 join_room，等待 OnRoomJoined 回调后导航
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("请求加入房间(网络): {RoomId}", _selectedRoomId);
            ServiceLocator.ClientService.RequestJoinRoom(_selectedRoomId);
        }
        else {
            _logger.LogWarning("加入房间失败: 未连接到服务器");
        }
    }

    /// <summary>
    /// 持久的事件处理器：大厅客户端收到 OnRoomCreated 时触发（网络模式创建房间成功）。
    /// </summary>
    private void OnRoomCreatedHandler(string createdRoomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("房间创建成功: {RoomId}", createdRoomId);
        CallDeferred(nameof(OnCreatedDeferred), createdRoomId);
    }

    /// <summary>
    /// 主线程处理房间创建成功回调，构造配置并进入准备界面。
    /// </summary>
    /// <param name="roomId">创建成功的房间 ID。</param>
    private void OnCreatedDeferred(string roomId) {
        if (_roomPreparation != null) {
            // 构造简单的 GameRoom config（Title 用缓存的房间名）
            var config = new GameRoom(roomId) {
                Title = _cachedCreateRoomId ?? roomId,
                HostName = ServiceLocator.ClientService.PlayerName,
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
            _cachedCreateRoomId = null;
            _roomPreparation.EnterRoom(roomId, config, isHost: true);
            NavigateTo(_roomPreparation);
        }
    }

    /// <summary>
    /// 持久的事件处理器：大厅客户端收到 OnRoomJoined 时触发（网络模式加入房间成功）。
    /// 准备阶段不重定向，直接进入 RoomPreparation 面板。
    /// </summary>
    private void OnRoomJoinedHandler(string joinedRoomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("成功加入房间: {RoomId}", joinedRoomId);
        CallDeferred(nameof(OnJoinedDeferred), joinedRoomId);
    }

    /// <summary>
    /// 主线程处理加入房间成功回调，进入准备界面。
    /// </summary>
    /// <param name="joinedRoomId">加入成功的房间 ID。</param>
    private void OnJoinedDeferred(string joinedRoomId) {
        if (_roomPreparation != null) {
            // 使用缓存的选中房间配置，或构造默认配置
            var config = _selectedRoomConfig ?? new GameRoom(joinedRoomId) {
                Title = joinedRoomId,
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
            _roomPreparation.EnterRoom(joinedRoomId, config, isHost: false);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("进入房间准备: {RoomId}", joinedRoomId);
            NavigateTo(_roomPreparation);
        }
    }

    /// <summary>
    /// 刷新房间列表：网络模式请求服务端，本地模式直接读取本地服务。
    /// </summary>
    private void OnRefreshRooms() {
        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过招募板协议请求房间列表
            ServiceLocator.ClientService.RequestListRooms();
        }
        else {
            // 本地模式：直接从本地服务获取
            var clientService = ClientService;
            if (clientService == null)
                return;

            var rooms = clientService.GetAllRooms().ToList();
            RefreshRoomList(rooms);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("房间列表刷新: {Count} 个房间", rooms.Count);
        }
    }

    /// <summary>
    /// 订阅大厅客户端的房间列表推送事件。
    /// </summary>
    private void SubscribeRoomListEvent() {
        ServiceLocator.ClientService.LobbyClient.OnRoomListReceived += (listings) => {
            _pendingRoomListings = listings;
            CallDeferred(nameof(OnRoomListingsReceivedDeferred));
        };
    }

    /// <summary>
    /// 主线程处理房间列表推送，转换为 GameRoom 并刷新 UI。
    /// </summary>
    private void OnRoomListingsReceivedDeferred() {
        var listings = _pendingRoomListings;
        _pendingRoomListings = null;
        if (listings == null)
            return;

        // 将 RoomListing 转换回 GameRoom 以适配现有 UI
        var rooms = listings.Select(l => new GameRoom(l.RoomId) {
            Title = l.Title,
            DungeonName = l.DungeonName,
            Category = l.Category,
            HostName = l.HostName,
            CurrentPlayers = l.CurrentPlayers,
            MaxPlayers = l.MaxPlayers,
            // Password 不通过列表传输，标记 HasPassword
            IsActive = l.Status != RoomStatus.Finished,
        }).ToList();

        RefreshRoomList(rooms);
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("招募板列表刷新: {Count} 个房间", rooms.Count);
    }

    #endregion
}
