using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Services;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle;

/// <summary>
/// 房间准备界面。玩家进入房间后选择阵营单位，准备就绪后开始战斗。
/// 准备阶段通过大厅 LobbyClient 的 JSON 协议进行单位增删和战斗启动，
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
    /// <summary>当前选择的阵营。</summary>
    private string _selectedCamp = CampConstants.CampA;
    /// <summary>当前选中的单位配置键。</summary>
    private string? _selectedUnitKey;
    /// <summary>已添加的单位显示名称列表。</summary>
    private readonly System.Collections.Generic.List<string> _units = [];

    // 可用单位配置（configKey → displayName & unitConfig）
    private static readonly System.Collections.Generic.Dictionary<string, (string displayName, UnitConfig config)> AvailableUnits = new() {
        ["WhiteMage"] = ("White Mage", GameConfigDB.UnitWhiteMage),
    };

    #endregion

    /// <summary>
    /// 节点就绪：绑定按钮与单位选择事件，订阅准备阶段单位列表推送。
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

        _logger.LogInformation("RoomPreparation ready");
    }

    /// <summary>
    /// 由 GameLobby 调用，设置房间信息并进入准备阶段。
    /// 网络模式通过 LobbyClient JSON 协议操作单位，本地模式通过 IClientBattleService。
    /// </summary>
    public void EnterRoom(string roomId, GameRoom? config = null) {
        _roomId = roomId;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("进入房间: {RoomId}", roomId);

        // 清空之前的单位列表
        _units.Clear();
        UpdateUnitList();

        // 显示招募板信息
        if (config != null) {
            // TitleLabel：金色大字标题
            if (InterRefs?.TitleLabel != null)
                InterRefs.TitleLabel.Text = string.IsNullOrEmpty(config.Title) ? roomId : config.Title;

            // RoomNameLabel：房主 / 类别 / 人数 副标题
            var roomLabelText = $"房主: {config.HostName}";
            if (config.Category != RoomCategory.Casual)
                roomLabelText += $"  |  {CategoryDisplayName(config.Category)}";
            roomLabelText += $"  |  {config.CurrentPlayers}/{config.MaxPlayers}人";
            if (InterRefs?.RoomNameLabel != null)
                InterRefs.RoomNameLabel.Text = roomLabelText;

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
            if (InterRefs?.RoomNameLabel != null)
                InterRefs.RoomNameLabel.Text = "";
            if (InterRefs?.InfoLabel != null)
                InterRefs.InfoLabel.Text = "";
            if (InterRefs?.StatusLabel != null)
                InterRefs.StatusLabel.Text = "请选择单位...";
        }

        InterRefs?.StartBattleButton?.Disabled = true;
    }

    /// <summary>
    /// 单位选择面板选择回调，记录选中单位并添加到列表。
    /// </summary>
    /// <param name="unitConfigKey">单位配置键。</param>
    private void OnUnitSelectedFromPanel(string unitConfigKey) {
        _selectedUnitKey = unitConfigKey;
        InterRefs?.StatusLabel?.Text = $"已选择: {AvailableUnits[unitConfigKey].displayName}";
        AddUnit();
    }

    /// <summary>
    /// 添加当前选中单位：网络模式发送 JSON 请求，本地模式直接创建并刷新列表。
    /// </summary>
    private void AddUnit() {
        if (string.IsNullOrEmpty(_selectedUnitKey))
            return;

        string displayName = AvailableUnits[_selectedUnitKey].displayName;
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
            UpdateUnitList();
            InterRefs?.StartBattleButton?.Disabled = _units.Count == 0;
        }

        InterRefs?.StatusLabel?.Text = $"请求创建 {displayName}...";
    }

    /// <summary>
    /// 服务器推送的准备阶段单位列表更新回调。
    /// </summary>
    private void OnPrepareUnitListUpdated(string eventRoomId, System.Collections.Generic.List<(string UnitName, string Camp)> units) {
        if (eventRoomId != _roomId)
            return;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("准备单位列表更新: {RoomId}, count={Count}", eventRoomId, units.Count);

        _units.Clear();

        foreach (var (unitName, _) in units) {
            _units.Add(unitName);
        }

        UpdateUnitList();
        InterRefs?.StartBattleButton?.Disabled = _units.Count == 0;
        InterRefs?.StatusLabel?.Text = $"单位列表已更新 ({_units.Count})";
    }

    /// <summary>
    /// 刷新单位列表文本显示。
    /// </summary>
    private void UpdateUnitList() {
        InterRefs?.UnitListLabel?.Text = "已选单位:\n" + (_units.Count > 0 ? string.Join("\n", _units) : "(空)");
    }

    /// <summary>
    /// 点击开始战斗按钮：校验单位非空后，网络模式发送请求，本地模式发出信号。
    /// </summary>
    private void OnStartBattleClicked() {
        if (_units.Count == 0) {
            InterRefs?.StatusLabel?.Text = "请先添加单位！";
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("请求开始战斗: {RoomId}, units={UnitCount}", _roomId, _units.Count);

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过大厅 LobbyClient JSON 协议发送 prepare_start_battle
            ServiceLocator.ClientService.LobbyClient.RequestPrepareStartBattle(_roomId);
        }
        else {
            // 本地模式：通过信号通知 GameLobby
            EmitSignal(SignalName.BattleStartRequested, _roomId);
        }

        Visible = false;
    }

    /// <summary>
    /// 将房间类别枚举转换为中文显示名。
    /// </summary>
    /// <param name="cat">房间类别。</param>
    /// <returns>对应的中文名称。</returns>
    private static string CategoryDisplayName(RoomCategory cat) => cat switch {
        RoomCategory.Casual => "休闲",
        RoomCategory.Competitive => "竞技",
        RoomCategory.Practice => "练习",
        RoomCategory.Tournament => "赛事",
        _ => "未知",
    };
}
