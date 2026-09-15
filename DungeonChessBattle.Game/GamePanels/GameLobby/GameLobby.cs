using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Lobby.Protocol.Dtos;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Game.ReplayUI;

namespace DungeonChessBattle.Game.GamePanels;

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

    /// <summary>回放入口面板引用，跨场景节点，由 MainScene 注入。</summary>
    [Export]
    private ReplayPanel? _replayPanel;

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
    /// <summary>缓存的服务端房间列表。</summary>
    private List<RoomListing>? _lastRoomListings;
    /// <summary>当前选中房间的列表配置。</summary>
    private RoomListing? _selectedRoomConfig;
    /// <summary>当前选中的副本键，创建房间时随配置下发。</summary>
    // 节点构造早于 MainScene._EnterTree 的内容装配，此处会命中未装配；留空，
    // 面板打开时由 PopulateDungeonSelect 填入。
    private string _selectedDungeonKey = "";

    #endregion

    /// <summary>
    /// 节点就绪：获取引用集合、连接按钮与准备界面信号，并订阅大厅客户端事件。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<GameLobbyInterRefs>("GameLobbyInterRefs");
        if (InterRefs is null) {
            _logger.LogError("GameLobbyInterRefs node not found.");
            return;
        }

        if (_roomPreparation == null)
            _logger.LogError("RoomPreparation reference is not assigned. Room preparation will be unavailable.");
        if (_replayPanel == null)
            _logger.LogError("ReplayPanel reference is not assigned. Replay list will be unavailable.");

        // 连接按钮信号
        InterRefs?.CreateButton?.Pressed += OnCreateRoom;
        InterRefs?.RefreshButton?.Pressed += OnRefreshRooms;
        InterRefs?.BackButton?.Pressed += GoBack;
        InterRefs?.PlaybackListButton?.Pressed += OnOpenReplayList;
        var joinBtn = InterRefs?.JoinButton;
        if (joinBtn is not null) {
            joinBtn.Pressed += OnJoinRoom;
            joinBtn.Disabled = true;
        }

        // 持久订阅大厅客户端事件（经 GameClientService 主线程派发）
        ServiceLocator.ClientService.OnRoomJoined += OnRoomJoinedHandler;
        ServiceLocator.ClientService.OnRoomCreated += OnRoomCreatedHandler;
        SubscribeRoomListEvent();

        PopulateDungeonSelect();

        _logger.LogInformation("GameLobby ready");
    }

    /// <summary>
    /// 从共享内容注册表的副本集合填充创建房间的副本下拉，并缓存选中键。
    /// 服务端据此生成对应敌人阵容，客户端据此呈现对应环境。
    /// </summary>
    private void PopulateDungeonSelect() {
        var select = InterRefs?.DungeonSelect;
        if (select == null)
            return;

        select.Clear();
        var dungeons = ServiceLocator.GameContent.Registry.Dungeons.ToList();
        for (int i = 0; i < dungeons.Count; i++) {
            var key = dungeons[i].DungeonKey;
            select.AddItem(ServiceLocator.ModAssets?.Dungeon(key)?.DisplayLabel ?? key.Value, i);
            select.SetItemMetadata(i, key.Value);
        }
        if (dungeons.Count > 0) {
            select.Selected = 0;
            OnDungeonSelected(0);
            select.ItemSelected += OnDungeonSelected;
        }
    }

    /// <summary>副本下拉选中回调：缓存选中副本键，用于创建房间配置。</summary>
    private void OnDungeonSelected(long index) {
        var metadata = InterRefs?.DungeonSelect?.GetItemMetadata((int)index);
        if (metadata is Variant variant && variant.VariantType == Variant.Type.String)
            _selectedDungeonKey = variant.AsString();
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Selected dungeon: {DungeonKey}", _selectedDungeonKey);
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
    /// 点击创建房间按钮：以当前选中副本为配置发送创建请求，房间 ID 由服务端生成。
    /// 未选中有效副本时不出请求：副本键必填，无值可回落。
    /// </summary>
    private void OnCreateRoom() {
        var dungeon = ServiceLocator.GameContent.Registry.GetDungeon(_selectedDungeonKey);
        if (dungeon is null) {
            _logger.LogWarning("创建房间失败: 未选中有效副本 {DungeonKey}", _selectedDungeonKey);
            if (InterRefs?.DetailLabel != null)
                InterRefs.DetailLabel.Text = "没有可选的副本，无法创建房间";
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("请求创建房间(网络): dungeon={DungeonKey}", _selectedDungeonKey);
        var config = new RoomConfigDto(
            DungeonKey: dungeon.DungeonKey,
            Description: ServiceLocator.ModAssets?.Dungeon(dungeon.DungeonKey)?.Description ?? string.Empty,
            MaxPlayers: 2);
        ServiceLocator.ClientService.RequestCreateRoom(config);
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
    /// 点击回放列表按钮：切到回放入口面板，列表由面板打开时自行刷新。
    /// </summary>
    private void OnOpenReplayList() {
        if (_replayPanel != null)
            NavigateTo(_replayPanel);
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
    /// 主线程处理房间创建成功回调，进入准备界面。房间信息不在此猜测，以服务端快照为准。
    /// </summary>
    /// <param name="roomId">创建成功的房间 ID。</param>
    private void OnCreatedDeferred(string roomId) {
        if (_roomPreparation != null) {
            _roomPreparation.EnterRoom(roomId, isHost: true);
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
    /// 主线程处理加入房间成功回调，进入准备界面：列表缓存带内容指纹，未命中时交给服务端快照渲染。
    /// </summary>
    /// <param name="joinedRoomId">加入成功的房间 ID。</param>
    private void OnJoinedDeferred(string joinedRoomId) {
        if (_roomPreparation != null) {
            _roomPreparation.EnterRoom(joinedRoomId, _selectedRoomConfig, isHost: false);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("进入房间准备: {RoomId}", joinedRoomId);
            NavigateTo(_roomPreparation);
        }
    }

    /// <summary>
    /// 刷新房间列表：请求服务端房间列表。
    /// </summary>
    // Godot CallDeferred 按方法名调用，须保持实例方法（非 static）
#pragma warning disable S2325
    private void OnRefreshRooms() {
        // 通过招募板协议请求服务端房间列表
        ServiceLocator.ClientService.RequestListRooms();
    }
#pragma warning restore S2325

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
    /// 在主线程处理房间列表推送，直接以 RoomListing DTO 刷新 UI。
    /// </summary>
    private void OnRoomListingsReceived(IReadOnlyList<RoomListing> listings) {
        // 直接以传输 DTO RoomListing 作为 UI 数据源，不再二次转换为本地模型
        RefreshRoomList([.. listings]);
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("招募板列表刷新: {Count} 个房间", listings.Count);
    }

    #endregion
}
