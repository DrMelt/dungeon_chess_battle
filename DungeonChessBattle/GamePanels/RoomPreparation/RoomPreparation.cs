using System.Collections.Generic;
using System.Linq;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Protocol.Dtos;
using DungeonChessBattle.Services;
using DungeonChessBattle.Protocol.Enums;
using DungeonChessBattle.GamePanels;

namespace DungeonChessBattle;

/// <summary>
/// 房间准备界面。玩家进入房间后选择阵营单位并准备，房主在全员准备后开始战斗。
/// 房主显示"开始战斗"（等待其他玩家全部准备），非房主显示"准备"/"取消准备"切换。
/// 准备阶段通过大厅 LobbyClient 的 JSON 协议进行单位增删、准备切换和战斗启动，
/// 战斗启动后服务端返回端口重定向，客户端切换到 RoomBattleClient 的 LES 连接。
/// </summary>
public partial class RoomPreparation : BaseGamePanel {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<RoomPreparation> _logger = ServiceLocator.GetLogger<RoomPreparation>();

    #region Service & State

    /// <summary>导出引用集合节点。</summary>
    public RoomPreparationInterRefs? InterRefs {
        get; private set;
    }
    /// <summary>当前房间 ID。</summary>
    private string _roomId = "";
    /// <summary>当前选择的阵营（当前仅支持 A 方 Camp_A，协议已预留 Camp_A/B/Boss）。</summary>
    private string _selectedCamp = CampConstants.CampA;
    /// <summary>当前选中的单位配置键。</summary>
    private string? _selectedUnitKey;
    /// <summary>已添加的单位显示名称列表。</summary>
    private readonly List<string> _units = [];

    /// <summary>当前玩家是否为房主。</summary>
    private bool _isHost;
    /// <summary>当前玩家是否为非房主且已点击准备。</summary>
    private bool _isReady;
    /// <summary>除房主外其他玩家是否都已准备（房主视角）。</summary>
    private bool _othersReady = true;

    /// <summary>房主玩家名（副标题展示）。</summary>
    private string _hostName = "";
    /// <summary>副本名（副标题展示）。</summary>
    private string _dungeonName = "";
    /// <summary>房间最大玩家数。</summary>
    private int _maxPlayers = 2;

    /// <summary>玩家名 → 已选择职业显示名（UnitGrid 按玩家展示）。</summary>
    private readonly Dictionary<string, string> _playerUnitNames = [];
    /// <summary>房间玩家快照（服务端权威，含准备标志，用于 UnitGrid 按玩家列卡与高亮）。</summary>
    private List<(string PlayerName, bool Ready)> _roomPlayers = [];

    #endregion

    /// <summary>
    /// 节点就绪：绑定按钮与单位选择事件，订阅准备阶段单位列表与准备状态推送。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<RoomPreparationInterRefs>("RoomPreparationInterRefs");
        if (InterRefs is null) {
            GD.PrintErr("[RoomPreparation] RoomPreparationInterRefs node not found.");
            return;
        }

        InterRefs?.SelectUnitButton?.Pressed += () => {
            // UnitSelectPanel 为本面板的子节点覆盖层，直接打开而不隐藏本面板
            InterRefs?.UnitSelectPanel?.OpenPanelFrom();
        };
        InterRefs?.BackButton?.Pressed += OnBackButtonPressed;
        var startBtn = InterRefs?.StartBattleButton;
        if (startBtn is not null) {
            startBtn.Pressed += OnStartBattleClicked;
            startBtn.Disabled = true;
        }

        // 订阅 UnitSelectPanel 的选择信号
        if (InterRefs?.UnitSelectPanel is not null)
            InterRefs.UnitSelectPanel.UnitSelected += OnUnitSelectedFromPanel;

        // 订阅主线程派发的房间快照（服务端组装单发：准备状态 + 单位 + 房间信息）
        ServiceLocator.ClientService.OnRoomSnapshotUpdated += OnRoomSnapshotUpdated;

        _logger.LogInformation("RoomPreparation ready");
    }

    /// <summary>
    /// 返回按钮：通知服务端离开房间（准备阶段主动退出），随后返回来源面板。
    /// 服务端据此移除成员，并在房主退出时转让房主、最后一人退出时删除房间。
    /// </summary>
    private void OnBackButtonPressed() {
        if (!string.IsNullOrEmpty(_roomId))
            ServiceLocator.ClientService.RequestLeaveRoom(_roomId);
        _roomId = ""; // 复位，避免离开后旧房间快照误应用（OnRoomSnapshotUpdated 按 _roomId 过滤）
        GoBack();
    }

    /// <summary>
    /// 由 GameLobby 调用，设置房间信息并进入准备阶段。
    /// 通过 LobbyClient JSON 协议操作单位与准备状态。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="config">房间配置（可为空）。</param>
    /// <param name="isHost">当前玩家是否为房主。</param>
    public void EnterRoom(string roomId, RoomListing? config = null, bool isHost = false) {
        _roomId = roomId;
        _isHost = isHost;
        _isReady = false;
        _othersReady = isHost;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("进入房间: {RoomId}, isHost={IsHost}", roomId, isHost);

        // 清空之前的单位列表
        _units.Clear();
        _playerUnitNames.Clear();
        _roomPlayers = [];

        // 显示招募板信息
        if (config != null) {
            // TitleLabel：金色大字标题
            if (InterRefs?.TitleLabel != null)
                InterRefs.TitleLabel.Text = string.IsNullOrEmpty(config.Title) ? roomId : config.Title;

            // 副标题三标签并列：房主 / 副本名 / 人数
            _hostName = config.HostName;
            _dungeonName = config.DungeonName;
            _maxPlayers = config.MaxPlayers;
            UpdateRoomInfoLabels(config.CurrentPlayers);

            // InfoLabel：描述文本
            if (InterRefs?.InfoLabel != null)
                InterRefs.InfoLabel.Text = config.Description;

            // StatusLabel：操作提示
            if (InterRefs?.StatusLabel != null)
                InterRefs.StatusLabel.Text = "请选择单位...";
        }
        else {
            if (InterRefs?.TitleLabel != null)
                InterRefs.TitleLabel.Text = $"房间: {roomId}";
            _hostName = "";
            _dungeonName = "";
            _maxPlayers = 2;
            UpdateRoomInfoLabels(0);
            if (InterRefs?.InfoLabel != null)
                InterRefs.InfoLabel.Text = "";
            if (InterRefs?.StatusLabel != null)
                InterRefs.StatusLabel.Text = "请选择单位...";
        }

        // 先以本地视角保底一张自己的占位卡，避免广播延迟导致网格空白；
        // 随后用最近一次权威快照覆盖为真实数据（重放修复"订阅晚于广播"的初始状态丢失）。
        _roomPlayers = [(ServiceLocator.ClientService.PlayerName, false)];

        // 用最近一次权威快照一次初始化（单位 + 准备状态 + 房间信息），未命中则保持本地占位。
        if (ServiceLocator.ClientService.GetRoomSnapshot(_roomId) is { } snapshot)
            ApplySnapshot(snapshot, isInitial: true);

        RefreshUnitGrid();
        RefreshStartButton();
    }

    /// <summary>
    /// 单位选择面板选择回调，记录选中单位并添加到列表。
    /// </summary>
    /// <param name="unitConfigKey">单位配置键。</param>
    private void OnUnitSelectedFromPanel(string unitConfigKey) {
        _selectedUnitKey = unitConfigKey;
        var entry = UnitCatalog.GetByKey(unitConfigKey);
        if (entry is not null)
            InterRefs?.StatusLabel?.Text = $"已选择: {entry.DisplayName}";
        AddUnit();
    }

    /// <summary>
    /// 添加当前选中单位：通过 LobbyClient JSON 协议发送。
    /// </summary>
    private void AddUnit() {
        if (string.IsNullOrEmpty(_selectedUnitKey))
            return;

        var entry = UnitCatalog.GetByKey(_selectedUnitKey);
        if (entry is null)
            return;
        string displayName = entry.DisplayName;
        string camp = _selectedCamp;

        // 通过大厅 SignalR 协议发送（经 GameClientService 统一入口）
        ServiceLocator.ClientService.RequestPrepareAddUnit(_roomId, displayName, camp);

        InterRefs?.StatusLabel?.Text = $"请求创建 {displayName}...";
        RefreshStartButton();
    }

    /// <summary>
    /// 订阅到的房间快照更新（GameClientService 已派发到主线程）。参数：房间 ID、完整快照。
    /// 房号不匹配时丢弃；匹配则统一应用权威快照刷新 UI。
    /// </summary>
    private void OnRoomSnapshotUpdated(string eventRoomId, RoomSnapshot snapshot) {
        if (eventRoomId != _roomId)
            return;
        ApplySnapshot(snapshot, isInitial: false);
    }

    /// <summary>
    /// 应用服务端权威快照到本地状态并刷新 UI（首次初始化与后续更新共用）。
    /// 快照合并了单位列表、准备状态与房间静态信息，无需再拼接或暂存。
    /// </summary>
    /// <param name="snapshot">房间权威快照。</param>
    /// <param name="isInitial">是否为进房首次初始化（不写覆盖性提示文案）。</param>
    private void ApplySnapshot(RoomSnapshot snapshot, bool isInitial) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("应用房间快照: {RoomId}, units={UnitCount}, players={PlayerCount}",
                snapshot.RoomId, snapshot.Units.Count, snapshot.Players.Count);

        // 单位列表
        _units.Clear();
        _playerUnitNames.Clear();
        foreach (var unit in snapshot.Units) {
            _units.Add(unit.UnitName);
            _playerUnitNames[unit.PlayerName] = unit.UnitName;
        }

        // 房间静态信息
        _hostName = snapshot.HostName;
        _dungeonName = snapshot.DungeonName;
        _maxPlayers = snapshot.MaxPlayers;

        // 准备状态与玩家快照
        var players = snapshot.Players.Select(p => (p.PlayerName, p.Ready)).ToList();
        string myName = ServiceLocator.ClientService.PlayerName;
        _isReady = false;
        foreach (var (playerName, ready) in players) {
            if (playerName == myName) {
                _isReady = ready;
                break;
            }
        }
        // 房主可能随原房主退出而转让：以服务端权威房主为准刷新本地身份，驱动按钮状态切换
        _isHost = snapshot.HostName == myName;
        _othersReady = _isHost || AllOthersReady(_hostName, players);
        _roomPlayers = players;

        UpdateRoomInfoLabels(snapshot.CurrentPlayers);
        RefreshUnitGrid();
        RefreshStartButton();

        if (!isInitial)
            InterRefs?.StatusLabel?.Text = $"单位列表已更新 ({_units.Count})";
    }

    /// <summary>判断除房主外所有玩家是否都已准备；无其他玩家时视为已满足。</summary>
    private static bool AllOthersReady(string hostName, List<(string PlayerName, bool Ready)> players) {
        foreach (var (playerName, ready) in players) {
            if (playerName == hostName)
                continue;
            if (!ready)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 刷新副标题三标签并列显示：房主 / 副本名 / 人数。
    /// </summary>
    /// <param name="currentPlayers">房间当前玩家数。</param>
    private void UpdateRoomInfoLabels(int currentPlayers) {
        if (InterRefs?.HostLabel != null)
            InterRefs.HostLabel.Text = string.IsNullOrEmpty(_hostName) ? "房主: --" : $"房主: {_hostName}";
        if (InterRefs?.DungeonNameLabel != null)
            InterRefs.DungeonNameLabel.Text = string.IsNullOrEmpty(_dungeonName) ? "副本: --" : $"副本: {_dungeonName}";
        if (InterRefs?.PlayersLabel != null)
            InterRefs.PlayersLabel.Text = $"人数: {currentPlayers}/{_maxPlayers}";
    }

    /// <summary>
    /// 按房间玩家刷新 UnitGrid 职业选择卡片。
    /// 已选择职业的玩家展示职业名，未选择的展示占位；已准备玩家卡片高亮。
    /// 玩家快照为空时退化为仅按已选单位归属的玩家列卡（处理 unit_list 早于 room_state 到达）。
    /// </summary>
    private void RefreshUnitGrid() {
        if (InterRefs?.UnitCardGrid is null || InterRefs?.UnitCardScene is null)
            return;

        // 玩家快照为空时退化为仅按已选单位归属的玩家列卡（处理 unit_list 早于 room_state 到达）
        var players = _roomPlayers;
        if (players.Count == 0) {
            players = [];
            foreach (var playerName in _playerUnitNames.Keys)
                players.Add((playerName, false));
        }

        // 清空旧卡片
        foreach (Node child in InterRefs.UnitCardGrid.GetChildren())
            child.QueueFree();

        foreach (var (playerName, ready) in players) {
            var card = InterRefs.UnitCardScene.Instantiate<UnitCard>();

            if (_playerUnitNames.TryGetValue(playerName, out string? unitDisplayName) && unitDisplayName != null
                && UnitCatalog.GetByDisplayName(unitDisplayName) is { } entry) {
                // 已选择职业：展示职业名 + 玩家名 + 真实 HP 数值
                card.SetupUnit(entry.ConfigKey, unitDisplayName, entry.Config.MaxHealth);
                card.SetUserName(playerName);
            }
            else {
                // 未选择职业或配置缺失：占位样式
                card.SetPlaceholder(playerName);
            }

            InterRefs.UnitCardGrid.AddChild(card);
            // 高亮在加入场景树后设置，确保 _refs 已就绪、背景色即时生效
            card.SetSelected(ready);
        }
    }

    /// <summary>
    /// 刷新底部主按钮的状态机：
    /// 房主显示"开始战斗"，可用条件为单位非空且除房主外其他玩家全部准备；
    /// 非房主显示"准备"/"取消准备"，点击后切换准备状态。
    /// </summary>
    private void RefreshStartButton() {
        var startBtn = InterRefs?.StartBattleButton;
        if (startBtn == null)
            return;

        if (_isHost) {
            startBtn.Text = "开始战斗";
            startBtn.Disabled = _units.Count == 0 || !_othersReady;
        }
        else {
            startBtn.Text = _isReady ? "取消准备" : "准备";
            startBtn.Disabled = _units.Count == 0;
        }
    }

    /// <summary>
    /// 点击底部主按钮：房主触发开始战斗，非房主切换准备/取消准备。
    /// </summary>
    private void OnStartBattleClicked() {
        if (_isHost) {
            OnStartBattleAsHost();
        }
        else {
            OnToggleReady();
        }
    }

    /// <summary>
    /// 房主点击开始战斗：校验单位与全员准备后，通过 LobbyClient 发送请求。
    /// </summary>
    private void OnStartBattleAsHost() {
        if (_units.Count == 0) {
            InterRefs?.StatusLabel?.Text = "请先添加单位！";
            return;
        }

        if (!_othersReady) {
            InterRefs?.StatusLabel?.Text = "等待其他玩家准备...";
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("请求开始战斗: {RoomId}, units={UnitCount}", _roomId, _units.Count);

        // 通过大厅 LobbyClient JSON 协议发送 prepare_start_battle
        ServiceLocator.ClientService.LobbyClient.RequestPrepareStartBattle(
            _roomId, ServiceLocator.ClientService.PlayerName, ServiceLocator.ClientService.PlayerId);

        Visible = false;
    }

    /// <summary>
    /// 非房主点击准备/取消准备：发送切换请求，等待服务端广播确认。
    /// </summary>
    private void OnToggleReady() {
        if (!ServiceLocator.ClientService.IsConnected)
            return;

        if (_isReady) {
            ServiceLocator.ClientService.LobbyClient.RequestPrepareUnready(_roomId, ServiceLocator.ClientService.PlayerName);
            InterRefs?.StatusLabel?.Text = "已取消准备";
        }
        else {
            ServiceLocator.ClientService.LobbyClient.RequestPrepareReady(_roomId, ServiceLocator.ClientService.PlayerName);
            InterRefs?.StatusLabel?.Text = "已请求准备...";
        }
    }

}
