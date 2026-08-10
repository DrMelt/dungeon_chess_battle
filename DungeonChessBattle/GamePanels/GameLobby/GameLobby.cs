using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
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

    #endregion

    #region State

    /// <summary>当前选中的房间 ID。</summary>
    private string? _selectedRoomId;
    /// <summary>房间 ID 到卡片节点的缓存。</summary>
    private readonly Dictionary<string, RoomInfo> _roomInfoCache = [];
    /// <summary>当前筛选的房间类别。</summary>
    private RoomCategory _selectedCategory = RoomCategory.Casual;
    /// <summary>缓存的服务端房间列表。</summary>
    private List<RoomListing>? _lastRoomListings;
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

        if (_roomPreparation == null)
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

        // 持久订阅大厅客户端事件（经 GameClientService 主线程派发）
        ServiceLocator.ClientService.OnRoomJoined += OnRoomJoinedHandler;
        ServiceLocator.ClientService.OnRoomCreated += OnRoomCreatedHandler;
        SubscribeRoomListEvent();

        _logger.LogInformation("GameLobby ready");
    }

    /// <summary>
    /// 面板每次被 NavigateTo 打开时触发一次延迟刷新，
    /// 给 Entity 同步留出时间（网络模式下 OnPeerConnected → Entity 构造需要数个 Tick）。
    /// </summary>
    protected override void OnPanelOpened() {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("GameLobby opened, connected={IsConnected}", ServiceLocator.ClientService.IsConnected);
        CallDeferred(nameof(OnRefreshRooms));
    }

    #region Button Handlers

    /// <summary>
    /// 点击创建房间按钮：校验房间名后，发送创建请求。
    /// </summary>
    private void OnCreateRoom() {
        string roomName = InterRefs?.RoomNameInput?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(roomName)) {
            _logger.LogWarning("创建房间失败: 房间名为空");
            return;
        }

        InterRefs?.RoomNameInput?.Clear();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("请求创建房间(网络): {RoomName}", roomName);
        _cachedCreateRoomId = roomName;
        ServiceLocator.ClientService.RequestCreateRoom(roomName);
    }

    /// <summary>
    /// 点击加入按钮：校验已选中房间后发送加入请求。
    /// </summary>
    private void OnJoinRoom() {
        if (string.IsNullOrEmpty(_selectedRoomId)) {
            _logger.LogWarning("加入房间失败: 未选中房间");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("请求加入房间(网络): {RoomId}", _selectedRoomId);
        ServiceLocator.ClientService.RequestJoinRoom(_selectedRoomId);
    }

    /// <summary>
    /// 持久的事件处理器：大厅客户端收到 OnRoomCreated 时触发（网络模式创建房间成功）。
    /// </summary>
    private void OnRoomCreatedHandler(string createdRoomId) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("房间创建成功: {RoomId}", createdRoomId);
        OnCreatedDeferred(createdRoomId);
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
        OnJoinedDeferred(joinedRoomId);
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
    /// 刷新房间列表：请求服务端房间列表。
    /// </summary>
    private void OnRefreshRooms() {
        // 通过招募板协议请求服务端房间列表
        ServiceLocator.ClientService.RequestListRooms();
    }

    /// <summary>
    /// 订阅大厅客户端的房间列表推送事件。
    /// </summary>
    private void SubscribeRoomListEvent() {
        // GameClientService 已派发到主线程，直接更新缓存并刷新 UI
        ServiceLocator.ClientService.OnRoomListReceived += (listings) => {
            _lastRoomListings = [.. listings];
            OnRoomListingsReceived(listings);
        };
    }

    /// <summary>
    /// 在主线程处理房间列表推送，转换为 GameRoom 并刷新 UI。
    /// </summary>
    private void OnRoomListingsReceived(IReadOnlyList<RoomListing> listings) {
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
