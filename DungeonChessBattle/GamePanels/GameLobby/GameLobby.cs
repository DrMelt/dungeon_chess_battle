using System.Collections.Generic;
using System.Linq;
using Godot;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 游戏大厅主控脚本，负责房间列表展示、创建/加入房间等操作。
/// 服务从 ServiceLocator 获取，不再由外部注入。
/// </summary>
public partial class GameLobby : BaseGamePanel {
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
        InterRefs?.BackButton?.Pressed += ClosePanel;
        var joinBtn = InterRefs?.JoinButton;
        if (joinBtn is not null) {
            joinBtn.Pressed += OnJoinRoom;
            joinBtn.Disabled = true;
        }

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
        // 防御性初始化：确保本地服务已就绪（防止面板在 InitLocalMode 之前被打开）
        if (!ServiceLocator.ClientService.IsConnected && ServiceLocator.ClientService.Client == null) {
            ServiceLocator.ClientService.InitLocalMode();
            GD.Print("[GameLobby] InitLocalMode called from OnPanelOpened (defensive).");
        }
        CallDeferred(nameof(OnRefreshRooms));
    }

    #region Button Handlers

    private void OnCreateRoom() {
        string roomName = InterRefs?.RoomNameInput?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(roomName)) {
            GD.Print("[GameLobby] Room name is empty.");
            return;
        }

        var clientService = ClientService;
        if (clientService == null) {
            GD.PrintErr("[GameLobby] Cannot create room: no service available.");
            return;
        }

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过 NetworkBattleClient 请求
            if (clientService is DungeonChessBattle.Client.NetworkBattleClient nbClient) {
                nbClient.RequestCreateRoom(roomName);
                GD.Print($"[GameLobby] Requested create room: {roomName}");
            }
        }
        else if (clientService is GameLogicService logicService) {
            // 本地模式
            logicService.CreateRoom(roomName);
            GD.Print($"[GameLobby] Local room created: {roomName}");
        }

        InterRefs?.RoomNameInput?.Clear();

        // 本地模式：创建后直接进入房间准备
        if (!ServiceLocator.ClientService.IsConnected && clientService is GameLogicService && _roomPreparation != null) {
            _roomPreparation.EnterRoom(roomName, clientService);
            NavigateTo(_roomPreparation);
        }
    }

    /// <summary>
    /// 公开方法：开始战斗。由 RoomPreparation 调用。
    /// </summary>
    public void StartBattle(string roomId) {
        if (ClientService == null) {
            GD.PrintErr("[GameLobby] Cannot start battle: no service.");
            return;
        }

        Visible = false;
        EmitSignal(SignalName.BattleStarted, roomId);
        GD.Print($"[GameLobby] Battle started for room: {roomId}");
    }

    private void OnJoinRoom() {
        if (string.IsNullOrEmpty(_selectedRoomId)) {
            GD.Print("[GameLobby] No room selected to join.");
            return;
        }

        if (ServiceLocator.ClientService.IsConnected && ClientService is DungeonChessBattle.Client.NetworkBattleClient nbClient) {
            var roomId = _selectedRoomId;

            void OnJoined(string joinedRoomId) {
                GD.Print($"[GameLobby] Joined room successfully: {joinedRoomId}");
                CallDeferred(nameof(OnJoinedDeferred), joinedRoomId);
                nbClient.OnRoomJoined -= OnJoined;
            }

            nbClient.OnRoomJoined += OnJoined;
            nbClient.RequestJoinRoom(roomId);
            GD.Print($"[GameLobby] Requested join room: {roomId}");
        }
        else {
            GD.PrintErr("[GameLobby] Cannot join room: not connected.");
        }
    }

    private void OnJoinedDeferred(string joinedRoomId) {
        if (_roomPreparation != null && ClientService is DungeonChessBattle.Client.NetworkBattleClient nbClient) {
            _roomPreparation.EnterRoom(joinedRoomId, nbClient);
            NavigateTo(_roomPreparation);
        }
    }

    private void OnRefreshRooms() {
        var clientService = ClientService;
        if (clientService == null)
            return;

        var rooms = clientService.GetAllRooms().ToList();
        RefreshRoomList(rooms);
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
