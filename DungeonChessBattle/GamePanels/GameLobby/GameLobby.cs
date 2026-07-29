using System.Collections.Generic;
using System.Linq;
using Godot;
using DungeonChessBattle.Client;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Services;

namespace DungeonChessBattle;

/// <summary>
/// 游戏大厅主控脚本，负责房间列表展示、创建/加入房间等操作。
/// 服务通过 SetClientService 注入，若未注入则自动以 Local 模式启动。
/// </summary>
public partial class GameLobby : BaseGamePanel {
    #region Signals

    /// <summary>
    /// 玩家进入房间时触发（创建或加入）。
    /// </summary>
    [Signal]
    public delegate void RoomEnteredEventHandler(string roomId);

    /// <summary>
    /// 战斗开始（从准备界面发起的请求）。
    /// </summary>
    [Signal]
    public delegate void BattleStartedEventHandler(string roomId);

    #endregion

    #region Service References

    private GameLobbyInterRefs? _interRefs;
    private IClientBattleService? _clientService;
    private NetworkBattleClient? _networkClient;

    /// <summary>
    /// 公开服务实例，供外部组件获取。
    /// </summary>
    public IClientBattleService? ClientService => _clientService;

    #endregion

    #region State

    private string? _selectedRoomId;
    private readonly Dictionary<string, RoomInfo> _roomInfoCache = [];
    private Timer? _refreshTimer;
    private bool _isInitialized;

    #endregion

    /// <summary>
    /// 外部注入战斗服务（主场景在启动时调用）。
    /// </summary>
    public void SetClientService(IClientBattleService service) {
        _clientService = service;

        if (service is NetworkBattleClient nbClient) {
            _networkClient = nbClient;
            GD.Print("[GameLobby] Network mode activated.");
        }
        else {
            GD.Print("[GameLobby] Local service set.");
        }

        _isInitialized = true;
    }

    public override void _Ready() {
        _interRefs = GetNode<GameLobbyInterRefs>("GameLobbyInterRefs");

        // 连接按钮信号
        _interRefs?.CreateButton?.Pressed += OnCreateRoom;
        _interRefs?.RefreshButton?.Pressed += OnRefreshRooms;
        if (_interRefs?.JoinButton is not null) {
            _interRefs.JoinButton.Pressed += OnJoinRoom;
            _interRefs.JoinButton.Disabled = true;
        }

        // 若外部未注入服务，则自动以 Local 模式备选
        if (!_isInitialized) {
            var localService = new GameLogicService();
            SetClientService(localService);
            GD.Print("[GameLobby] Initialized in Local mode (auto fallback).");
        }

        // 启动定时刷新
        _refreshTimer = new Timer {
            WaitTime = 1.0,
            OneShot = false,
            Autostart = true,
        };
        _refreshTimer.Timeout += OnRefreshRooms;
        AddChild(_refreshTimer);

        // 首次刷新
        OnRefreshRooms();
    }

    #region Button Handlers

    private void OnCreateRoom() {
        string roomName = _interRefs?.RoomNameInput?.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(roomName)) {
            GD.Print("[GameLobby] Room name is empty.");
            return;
        }

        if (_networkClient != null && _networkClient.IsConnected) {
            _networkClient.RequestCreateRoom(roomName);
            GD.Print($"[GameLobby] Requested create room: {roomName}");
        }
        else if (_clientService is GameLogicService logicService) {
            logicService.CreateRoom(roomName);
            GD.Print($"[GameLobby] Local room created: {roomName}");
        }
        else {
            GD.PrintErr("[GameLobby] Cannot create room: not connected and not in local mode.");
        }

        _interRefs?.RoomNameInput?.Clear();

        // 本地模式：创建后直接进入房间准备
        if (_clientService is GameLogicService) {
            EmitSignal(SignalName.RoomEntered, roomName);
        }
    }

    /// <summary>
    /// 公开方法：开始战斗。由 RoomPreparation 调用。
    /// </summary>
    public void StartBattle(string roomId) {
        if (_clientService == null) {
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

        if (_networkClient != null && _networkClient.IsConnected) {
            _networkClient.RequestJoinRoom(_selectedRoomId);
            GD.Print($"[GameLobby] Requested join room: {_selectedRoomId}");
        }
        else {
            GD.PrintErr("[GameLobby] Cannot join room: not connected.");
        }
    }

    private void OnRefreshRooms() {
        if (!_isInitialized || _clientService == null)
            return;

        var rooms = _clientService.GetAllRooms().ToList();
        RefreshRoomList(rooms);
    }

    #endregion

    #region Room List UI

    private void RefreshRoomList(List<GameRoom> rooms) {
        if (_interRefs?.RoomListContainer == null)
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
                _interRefs.RoomListContainer.RemoveChild(node);
                node.QueueFree();
                _roomInfoCache.Remove(roomId);
            }
        }

        // 添加/更新房间卡片
        foreach (var room in rooms) {
            if (!_roomInfoCache.TryGetValue(room.RoomId, out var roomInfo)) {
                roomInfo = CreateRoomInfoCard(room.RoomId);
                _interRefs.RoomListContainer.AddChild(roomInfo);
                _roomInfoCache[room.RoomId] = roomInfo;
            }

            string statusText = GetRoomStatusText(room);
            roomInfo.UpdateStatus(statusText);
        }
    }

    private RoomInfo CreateRoomInfoCard(string roomId) {
        if (_interRefs?.RoomInfoScene is null)
            throw new System.InvalidOperationException("RoomInfoScene is not assigned.");
        var instance = _interRefs.RoomInfoScene.Instantiate<RoomInfo>();
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
        if (_interRefs?.DetailLabel != null) {
            _interRefs.DetailLabel.Text = $"选中房间: {roomId}\n";
            if (_clientService != null) {
                var gameRoom = _clientService.GetRoom(roomId);
                if (gameRoom != null) {
                    _interRefs.DetailLabel.Text += $"阵营A单位: {gameRoom.UnitsA.Count}\n";
                    _interRefs.DetailLabel.Text += $"阵营B单位: {gameRoom.UnitsB.Count}\n";
                }
            }
        }

        // 启用加入按钮
        if (_interRefs?.JoinButton is not null)
            _interRefs.JoinButton.Disabled = false;
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
