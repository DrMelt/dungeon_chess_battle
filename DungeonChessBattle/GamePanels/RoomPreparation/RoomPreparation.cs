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
    private readonly ILogger<RoomPreparation> _logger = ServiceLocator.GetLogger<RoomPreparation>();

    [Signal]
    public delegate void BattleStartRequestedEventHandler(string roomId);

    #region Service & State

    public RoomPreparationInterRefs? InterRefs {
        get; private set;
    }
    private string _roomId = "";
    private string _selectedCamp = "Camp_A";
    private string? _selectedUnitKey;
    private readonly System.Collections.Generic.List<string> _units = [];

    // 可用单位配置（configKey → displayName & unitConfig）
    private static readonly System.Collections.Generic.Dictionary<string, (string displayName, UnitConfig config)> AvailableUnits = new() {
        ["WhiteMage"] = ("White Mage", GameConfigDB.UnitWhiteMage),
    };

    #endregion

    public override void _Ready() {
        InterRefs = GetNode<RoomPreparationInterRefs>("RoomPreparationInterRefs");
        if (InterRefs is null) {
            GD.PrintErr("[RoomPreparation] RoomPreparationInterRefs node not found.");
            return;
        }

        InterRefs?.SelectUnitButton?.Pressed += () => {
            if (InterRefs?.UnitSelectPanel is not null)
                NavigateTo(InterRefs.UnitSelectPanel);
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

    private void OnUnitSelectedFromPanel(string unitConfigKey) {
        _selectedUnitKey = unitConfigKey;
        InterRefs?.StatusLabel?.Text = $"已选择: {AvailableUnits[unitConfigKey].displayName}";
        AddUnit();
    }

    private void AddUnit() {
        if (string.IsNullOrEmpty(_selectedUnitKey))
            return;

        string displayName = AvailableUnits[_selectedUnitKey].displayName;
        byte camp = _selectedCamp == "Camp_A" ? (byte)1 : (byte)2;

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
    private void OnPrepareUnitListUpdated(string eventRoomId, System.Collections.Generic.List<(string UnitName, byte Camp)> units) {
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

    private void UpdateUnitList() {
        InterRefs?.UnitListLabel?.Text = "已选单位:\n" + (_units.Count > 0 ? string.Join("\n", _units) : "(空)");
    }

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

    private static string CategoryDisplayName(RoomCategory cat) => cat switch {
        RoomCategory.Casual => "休闲",
        RoomCategory.Competitive => "竞技",
        RoomCategory.Practice => "练习",
        RoomCategory.Tournament => "赛事",
        _ => "未知",
    };
}
