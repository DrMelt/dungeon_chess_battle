using System.Collections.Generic;
using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Services;
using DungeonChessBattle.Core.Enums;
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

    /// <summary>本地模式请求开始战斗的信号，参数为房间 ID。</summary>
    [Signal]
    public delegate void BattleStartRequestedEventHandler(string roomId);

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
    /// <summary>除房主外其他玩家是否都已准备（房主视角）或本地模式固定 true。</summary>
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
        InterRefs?.BackButton?.Pressed += GoBack;
        var startBtn = InterRefs?.StartBattleButton;
        if (startBtn is not null) {
            startBtn.Pressed += OnStartBattleClicked;
            startBtn.Disabled = true;
        }

        // 订阅 UnitSelectPanel 的选择信号
        if (InterRefs?.UnitSelectPanel is not null)
            InterRefs.UnitSelectPanel.UnitSelected += OnUnitSelectedFromPanel;

        // 持久订阅大厅准备阶段单位列表推送
        ServiceLocator.ClientService.LobbyClient.OnPrepareUnitListUpdated += OnPrepareUnitListUpdated;

        // 订阅大厅准备阶段房间准备状态推送（房主名与各玩家准备标志）
        ServiceLocator.ClientService.LobbyClient.OnPrepareRoomStateUpdated += OnPrepareRoomStateUpdated;

        _logger.LogInformation("RoomPreparation ready");
    }

    /// <summary>
    /// 由 GameLobby 调用，设置房间信息并进入准备阶段。
    /// 网络模式通过 LobbyClient JSON 协议操作单位与准备状态，本地模式通过 IClientBattleService。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="config">房间配置（可为空）。</param>
    /// <param name="isHost">当前玩家是否为房主。</param>
    public void EnterRoom(string roomId, GameRoom? config = null, bool isHost = false) {
        _roomId = roomId;
        _isHost = isHost;
        _isReady = false;
        // 本地模式无其他玩家：默认视为其他玩家已全准备
        _othersReady = !ServiceLocator.ClientService.IsConnected || isHost;
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

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：先以本地视角保底一张自己的占位卡，避免广播延迟导致网格空白；
            // 随后用最近一次广播缓存覆盖为权威数据（重放修复"订阅晚于广播"的初始状态丢失）。
            _roomPlayers = [(ServiceLocator.ClientService.PlayerName, false)];

            if (ServiceLocator.ClientService.LobbyClient.TryGetRecentUnitList(_roomId, out var cachedUnits)) {
                _units.Clear();
                _playerUnitNames.Clear();
                foreach (var (unitName, _, playerName) in cachedUnits) {
                    _units.Add(unitName);
                    _playerUnitNames[playerName] = unitName;
                }
            }

            if (ServiceLocator.ClientService.LobbyClient.TryGetRecentRoomState(_roomId,
                    out var cachedHostName, out var cachedDungeonName, out var cachedPlayers)) {
                _roomPlayers = cachedPlayers;
                _hostName = cachedHostName;
                _dungeonName = cachedDungeonName;
                UpdateRoomInfoLabels(cachedPlayers.Count);
            }
        }
        else {
            // 本地模式：房间内仅自己，初始化一张未选择职业的卡片
            _roomPlayers = [(ServiceLocator.ClientService.PlayerName, false)];
        }

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
    /// 添加当前选中单位：网络模式发送 JSON 请求，本地模式直接创建并刷新列表。
    /// </summary>
    private void AddUnit() {
        if (string.IsNullOrEmpty(_selectedUnitKey))
            return;

        var entry = UnitCatalog.GetByKey(_selectedUnitKey);
        if (entry is null)
            return;
        string displayName = entry.DisplayName;
        string camp = _selectedCamp;

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过大厅 LobbyClient JSON 协议发送
            ServiceLocator.ClientService.LobbyClient.RequestPrepareAddUnit(_roomId, displayName, camp);
        }
        else {
            // 本地模式：直接通过 IClientBattleService
            var client = ServiceLocator.ClientService.Client;
            client?.CreateUnit(_roomId, displayName, camp);
            _units.Add(displayName);
            _playerUnitNames[ServiceLocator.ClientService.PlayerName] = displayName;
            RefreshUnitGrid();
        }

        InterRefs?.StatusLabel?.Text = $"请求创建 {displayName}...";
        RefreshStartButton();
    }

    /// <summary>
    /// 服务器推送的准备阶段单位列表更新回调。
    /// </summary>
    private void OnPrepareUnitListUpdated(string eventRoomId, List<(string UnitName, string Camp, string PlayerName)> units) {
        if (eventRoomId != _roomId)
            return;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("准备单位列表更新: {RoomId}, count={Count}", eventRoomId, units.Count);

        _units.Clear();
        _playerUnitNames.Clear();

        foreach (var (unitName, _, playerName) in units) {
            _units.Add(unitName);
            _playerUnitNames[playerName] = unitName;
        }

        RefreshUnitGrid();
        RefreshStartButton();
        InterRefs?.StatusLabel?.Text = $"单位列表已更新 ({_units.Count})";
    }

    /// <summary>
    /// 服务器推送的当前玩家准备状态回调：更新自己的准备标志与其他玩家的准备进度。
    /// </summary>
    private void OnPrepareRoomStateUpdated(string eventRoomId, string hostName, string dungeonName, List<(string PlayerName, bool Ready)> players) {
        if (eventRoomId != _roomId)
            return;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("准备状态更新: {RoomId}, host={HostName}, players={Count}", eventRoomId, hostName, players.Count);

        // 同步自己的准备标志
        string myName = ServiceLocator.ClientService.PlayerName;
        foreach (var (playerName, ready) in players) {
            if (playerName == myName) {
                _isReady = ready;
                break;
            }
        }

        // 计算除房主外其他玩家是否全部准备
        _othersReady = !ServiceLocator.ClientService.IsConnected
            || _isHost
            || AllOthersReady(hostName, players);

        // 同步副标题展示（用服务端权威的房主名与副本名）
        _hostName = hostName;
        _dungeonName = dungeonName;
        UpdateRoomInfoLabels(players.Count);

        // 同步玩家快照与准备状态，按玩家刷新职业选择网格
        _roomPlayers = players;
        RefreshUnitGrid();

        RefreshStartButton();
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
            startBtn.Disabled = _units.Count == 0 || !ServiceLocator.ClientService.IsConnected;
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
    /// 房主点击开始战斗：校验单位与全员准备后，网络模式发送请求，本地模式发出信号。
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

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过大厅 LobbyClient JSON 协议发送 prepare_start_battle
            ServiceLocator.ClientService.LobbyClient.RequestPrepareStartBattle(
                _roomId, ServiceLocator.ClientService.PlayerName, ServiceLocator.ClientService.PlayerId);
        }
        else {
            // 本地模式：通过信号通知 GameLobby
            EmitSignal(SignalName.BattleStartRequested, _roomId);
        }

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
