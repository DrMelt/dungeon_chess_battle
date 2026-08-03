using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 游戏大厅主控脚本，负责房间列表展示、创建/加入房间等操作。
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

        // 持久订阅大厅客户端的房间加入事件（不再在 OnJoinRoom 中临时绑定）
        ServiceLocator.ClientService.LobbyClient.OnRoomJoined += OnRoomJoinedHandler;

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
        _logger.LogInformation("GameLobby opened, connected={IsConnected}", ServiceLocator.ClientService.IsConnected);
        // 防御性初始化：确保本地服务已就绪（防止面板在 InitLocalMode 之前被打开）
        if (!ServiceLocator.ClientService.IsConnected && ServiceLocator.ClientService.Client == null) {
            ServiceLocator.ClientService.InitLocalMode();
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

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过 GameClientService 发送含完整身份信息的创建请求
            _logger.LogInformation("请求创建房间(网络): {RoomName}", roomName);
            ServiceLocator.ClientService.RequestCreateRoom(roomName);
        }
        else {
            // 本地模式：通过接口创建
            var clientService = ClientService;
            if (clientService != null) {
                clientService.CreateRoom(roomName);
                _logger.LogInformation("本地房间创建成功: {RoomName}", roomName);
            }
            else {
                _logger.LogError("创建房间失败: 无可用服务");
                return;
            }
        }

        InterRefs?.RoomNameInput?.Clear();

        // 本地模式：创建后直接进入房间准备
        if (!ServiceLocator.ClientService.IsConnected && ClientService != null && _roomPreparation != null) {
            _roomPreparation.EnterRoom(roomName, ClientService);
            NavigateTo(_roomPreparation);
        }
    }

    /// <summary>
    /// 公开方法：开始战斗。由 RoomPreparation 调用。
    /// </summary>
    public void StartBattle(string roomId) {
        var clientService = ClientService;
        if (clientService == null) {
            _logger.LogError("启动战斗失败: 房间 {RoomId} 无可用服务", roomId);
            return;
        }

        // 通过接口调用（网络模式通过 RPC 发送，本地模式直接调用内部逻辑）
        clientService.RequestStartBattle(roomId);
        _logger.LogInformation("请求开始战斗: {RoomId}", roomId);

        Visible = false;
        EmitSignal(SignalName.BattleStarted, roomId);
        _logger.LogInformation("战斗已开始: {RoomId}", roomId);
    }

    private void OnJoinRoom() {
        if (string.IsNullOrEmpty(_selectedRoomId)) {
            _logger.LogWarning("加入房间失败: 未选中房间");
            return;
        }

        if (ServiceLocator.ClientService.IsConnected) {
            // 通过持久的大厅客户端发送请求（OnRoomJoined 回调已在 _Ready 中持久订阅）
            _logger.LogInformation("请求加入房间(网络): {RoomId}", _selectedRoomId);
            ServiceLocator.ClientService.RequestJoinRoom(_selectedRoomId);
        }
        else {
            _logger.LogWarning("加入房间失败: 未连接到服务器");
        }
    }

    /// <summary>
    /// 持久的事件处理器：大厅客户端收到 OnRoomJoined 时（重定向后房间端口连接成功）触发。
    /// </summary>
    private void OnRoomJoinedHandler(string joinedRoomId) {
        _logger.LogInformation("成功加入房间: {RoomId}", joinedRoomId);
        CallDeferred(nameof(OnJoinedDeferred), joinedRoomId);
    }

    private void OnJoinedDeferred(string joinedRoomId) {
        if (_roomPreparation != null) {
            // 使用持久的房间客户端（已通过 Reconnect 连接到房间端口）
            _roomPreparation.EnterRoom(joinedRoomId, ServiceLocator.ClientService.RoomClient);
            _logger.LogInformation("进入房间准备: {RoomId}", joinedRoomId);
            NavigateTo(_roomPreparation);
        }
    }

    private void OnRefreshRooms() {
        var clientService = ClientService;
        if (clientService == null)
            return;

        var rooms = clientService.GetAllRooms().ToList();
        RefreshRoomList(rooms);
        _logger.LogDebug("房间列表刷新: {Count} 个房间", rooms.Count);
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

        // 高亮当前选中
        if (_roomInfoCache.TryGetValue(roomId, out var current)) {
            current.SetSelected(true);
        }

        // 更新详情面板
        if (InterRefs?.DetailLabel != null) {
            InterRefs.DetailLabel.Text = $"选中房间: {roomId}\n";
            var clientService = ClientService;
            if (clientService != null) {
                var gameRoom = clientService.GetRoom(roomId);
                if (gameRoom != null) {
                    InterRefs.DetailLabel.Text += $"阵营A单位: {gameRoom.UnitsA.Count}\n";
                    InterRefs.DetailLabel.Text += $"阵营B单位: {gameRoom.UnitsB.Count}\n";
                }
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
