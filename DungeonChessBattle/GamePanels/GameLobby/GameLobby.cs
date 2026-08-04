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
/// </summary>
public partial class GameLobby : BaseGamePanel {
    private readonly ILogger<GameLobby> _logger = ServiceLocator.GetLogger<GameLobby>();

    #region Signals

    /// <summary>
    /// 战斗开始（从准备界面发起的请求）。
    /// </summary>
    [Signal]
    public delegate void BattleStartedEventHandler(string roomId);

    #endregion

    #region References

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

    private string? _selectedRoomId;
    private readonly Dictionary<string, RoomInfo> _roomInfoCache = [];
    private Timer? _refreshTimer;
    private RoomCategory _selectedCategory = RoomCategory.Casual;
    private List<RoomListing>? _pendingRoomListings;
    private string? _cachedCreateRoomId;
    private GameRoom? _selectedRoomConfig;

    #endregion

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

        _logger.LogInformation("GameLobby ready");

        // 启动定时刷新
        _refreshTimer = new Timer {
            WaitTime = 1.0,
            OneShot = false,
            Autostart = true,
        };
        _refreshTimer.Timeout += OnRefreshRooms;
        AddChild(_refreshTimer);

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
                    _roomPreparation.EnterRoom(roomName, localConfig);
                    NavigateTo(_roomPreparation);
                }
            }
            else {
                _logger.LogError("创建房间失败: 无可用服务");
            }
        }
    }

    /// <summary>
    /// 公开方法：开始战斗（本地模式）。由 RoomPreparation 调用。
    /// 网络模式下 RoomPreparation 直接通过 LobbyClient.RequestPrepareStartBattle 发送。
    /// </summary>
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
            _roomPreparation.EnterRoom(roomId, config);
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

    private void OnJoinedDeferred(string joinedRoomId) {
        if (_roomPreparation != null) {
            // 使用缓存的选中房间配置，或构造默认配置
            var config = _selectedRoomConfig ?? new GameRoom(joinedRoomId) {
                Title = joinedRoomId,
                MaxPlayers = 2,
                CurrentPlayers = 1,
            };
            _roomPreparation.EnterRoom(joinedRoomId, config);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("进入房间准备: {RoomId}", joinedRoomId);
            NavigateTo(_roomPreparation);
        }
    }

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

    private void SubscribeRoomListEvent() {
        ServiceLocator.ClientService.LobbyClient.OnRoomListReceived += (listings) => {
            _pendingRoomListings = listings;
            CallDeferred(nameof(OnRoomListingsReceivedDeferred));
        };
    }

    private void OnRoomListingsReceivedDeferred() {
        var listings = _pendingRoomListings;
        _pendingRoomListings = null;
        if (listings == null)
            return;

        // 将 RoomListing 转换回 GameRoom 以适配现有 UI
        var rooms = listings.Select(l => new GameRoom(l.RoomId) {
            Title = l.Title,
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

    #region Room List UI

    private void RefreshRoomList(List<GameRoom> rooms) {
        if (InterRefs?.RoomListContainer == null)
            return;

        var currentRoomIds = rooms.Select(r => r.RoomId).ToHashSet();

        // 移除已不存在的房间
        var toRemove = new List<string>();
        foreach (var (roomId, _) in _roomInfoCache) {
            if (!currentRoomIds.Contains(roomId)) {
                toRemove.Add(roomId);
            }
        }
        foreach (var roomId in toRemove) {
            if (_roomInfoCache.TryGetValue(roomId, out var node)) {
                InterRefs.RoomListContainer.RemoveChild(node);
                node.QueueFree();
                _roomInfoCache.Remove(roomId);
            }
        }

        // 添加/更新房间卡片
        foreach (var room in rooms) {
            if (!_roomInfoCache.TryGetValue(room.RoomId, out var roomInfo)) {
                roomInfo = CreateRoomInfoCard(room.RoomId);
                InterRefs.RoomListContainer.AddChild(roomInfo);
                _roomInfoCache[room.RoomId] = roomInfo;
            }

            string statusText = GetRoomStatusText(room);
            roomInfo.UpdateStatus(statusText);
        }

        // 空状态提示
        if (rooms.Count == 0 && InterRefs?.DetailLabel != null) {
            InterRefs.DetailLabel.Text = "当前没有房间\n\n使用左侧面板创建一个房间吧！";
        }
    }

    private RoomInfo CreateRoomInfoCard(string roomId) {
        if (InterRefs?.RoomInfoScene is null)
            throw new System.InvalidOperationException("RoomInfoScene is not assigned.");
        var instance = InterRefs.RoomInfoScene.Instantiate<RoomInfo>();
        instance.Setup(roomId, "等待中");
        instance.RoomSelected += OnRoomSelected;
        return instance;
    }

    private void OnRoomSelected(string roomId) {
        // 取消上一个选中
        if (_selectedRoomId != null && _roomInfoCache.TryGetValue(_selectedRoomId, out var prev)) {
            prev.SetSelected(false);
        }

        _selectedRoomId = roomId;
        _selectedRoomConfig = null;

        // 高亮当前选中
        if (_roomInfoCache.TryGetValue(roomId, out var current)) {
            current.SetSelected(true);
        }

        // 更新详情面板并从缓存的 listing 中获取配置
        if (InterRefs?.DetailLabel != null) {
            var listing = _pendingRoomListings?.FirstOrDefault(r => r.RoomId == roomId);
            if (listing != null) {
                _selectedRoomConfig = new GameRoom(listing.RoomId) {
                    Title = listing.Title,
                    Category = listing.Category,
                    HostName = listing.HostName,
                    CurrentPlayers = listing.CurrentPlayers,
                    MaxPlayers = listing.MaxPlayers,
                };
                InterRefs.DetailLabel.Text = $"房间: {listing.Title}\n房主: {listing.HostName}\n类别: {listing.Category}\n人数: {listing.CurrentPlayers}/{listing.MaxPlayers}";
            }
            else {
                InterRefs.DetailLabel.Text = $"选中房间: {roomId}\n";
            }
        }

        // 启用加入按钮
        InterRefs?.JoinButton?.Disabled = false;
    }

    private static string GetRoomStatusText(GameRoom room) {
        if (room.IsActive) {
            int totalUnits = room.UnitsA.Count + room.UnitsB.Count;
            if (totalUnits > 0) {
                return $"等待中 ({room.UnitsA.Count}v{room.UnitsB.Count})";
            }
            return "等待中";
        }
        return "已结束";
    }

    #endregion

    public override void _ExitTree() {
        _refreshTimer?.Stop();
    }
}
