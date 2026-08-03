using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Services;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

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
    private EnumCamp _selectedCamp = EnumCamp.Camp_A;
    private string? _selectedUnitKey;
    private readonly System.Collections.Generic.List<string> _campAUnits = [];
    private readonly System.Collections.Generic.List<string> _campBUnits = [];

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

        InterRefs?.CampAButton?.Pressed += () => SelectCamp(EnumCamp.Camp_A);
        InterRefs?.CampBButton?.Pressed += () => SelectCamp(EnumCamp.Camp_B);
        InterRefs?.BackButton?.Pressed += GoBack;
        var startBtn = InterRefs?.StartBattleButton;
        if (startBtn is not null) {
            startBtn.Pressed += OnStartBattleClicked;
            startBtn.Disabled = true;
        }

        // 持久订阅大厅准备阶段单位列表推送
        ServiceLocator.ClientService.LobbyClient.OnPrepareUnitListUpdated += OnPrepareUnitListUpdated;

        SelectCamp(EnumCamp.Camp_A);
        PopulateUnitCards();
        _logger.LogInformation("RoomPreparation ready");
    }

    /// <summary>
    /// 由 GameLobby 调用，设置房间信息并进入准备阶段。
    /// 网络模式通过 LobbyClient JSON 协议操作单位，本地模式通过 IClientBattleService。
    /// </summary>
    public void EnterRoom(string roomId, GameRoom? config = null) {
        _roomId = roomId;
        _logger.LogInformation("进入房间: {RoomId}", roomId);

        // 清空之前的单位列表
        _campAUnits.Clear();
        _campBUnits.Clear();
        UpdateCampLists();

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

    private void PopulateUnitCards() {
        if (InterRefs?.UnitCardGrid is null || InterRefs?.UnitCardScene is null)
            return;

        foreach (Node child in InterRefs.UnitCardGrid.GetChildren())
            child.QueueFree();

        foreach (var (key, (displayName, config)) in AvailableUnits) {
            var card = InterRefs.UnitCardScene.Instantiate<UnitSelectCard>();
            string stats = $"HP: {config.MaxHealth:F0}  SPD: {config.BaseSpeed:F1}";
            card.Setup(key, displayName, stats);
            card.UnitSelected += OnUnitSelected;
            InterRefs.UnitCardGrid.AddChild(card);
        }
    }

    private void OnUnitSelected(string unitConfigKey) {
        _selectedUnitKey = unitConfigKey;
        InterRefs?.StatusLabel?.Text = $"已选择: {AvailableUnits[unitConfigKey].displayName} (将加入 {CampName(_selectedCamp)})";
        AddUnitToCamp();
    }

    private void SelectCamp(EnumCamp camp) {
        _selectedCamp = camp;
        InterRefs?.CampAButton?.ButtonPressed = camp == EnumCamp.Camp_A;
        InterRefs?.CampBButton?.ButtonPressed = camp == EnumCamp.Camp_B;
        InterRefs?.StatusLabel?.Text = $"当前阵营: {CampName(camp)} - 点击单位卡片添加";
    }

    private void AddUnitToCamp() {
        if (string.IsNullOrEmpty(_selectedUnitKey))
            return;

        string displayName = AvailableUnits[_selectedUnitKey].displayName;
        byte camp = _selectedCamp == EnumCamp.Camp_A ? (byte)1 : (byte)2;

        if (ServiceLocator.ClientService.IsConnected) {
            // 网络模式：通过大厅 LobbyClient JSON 协议发送
            ServiceLocator.ClientService.LobbyClient.RequestPrepareAddUnit(_roomId, displayName, camp);
        }
        else {
            // 本地模式：直接通过 IClientBattleService
            var client = ServiceLocator.ClientService.Client;
            client?.CreateUnit(_roomId, displayName, camp);
            // 本地模式同步更新列表
            if (camp == 1) _campAUnits.Add(displayName);
            else _campBUnits.Add(displayName);
            UpdateCampLists();
            InterRefs?.StartBattleButton?.Disabled = _campAUnits.Count == 0 && _campBUnits.Count == 0;
        }

        InterRefs?.StatusLabel?.Text = $"请求创建 {displayName} ({CampName(_selectedCamp)})...";
    }

    /// <summary>
    /// 服务器推送的准备阶段单位列表更新回调。
    /// </summary>
    private void OnPrepareUnitListUpdated(string eventRoomId, System.Collections.Generic.List<(string UnitName, byte Camp)> units) {
        if (eventRoomId != _roomId)
            return;

        _logger.LogInformation("准备单位列表更新: {RoomId}, count={Count}", eventRoomId, units.Count);

        _campAUnits.Clear();
        _campBUnits.Clear();

        foreach (var (unitName, camp) in units) {
            if (camp == 1) _campAUnits.Add(unitName);
            else _campBUnits.Add(unitName);
        }

        UpdateCampLists();
        InterRefs?.StartBattleButton?.Disabled = _campAUnits.Count == 0 && _campBUnits.Count == 0;
        InterRefs?.StatusLabel?.Text = $"单位列表已更新 (A:{_campAUnits.Count} B:{_campBUnits.Count})";
    }

    private void UpdateCampLists() {
        InterRefs?.CampAListLabel?.Text = "阵营 A:\n" + (_campAUnits.Count > 0 ? string.Join("\n", _campAUnits) : "(空)");
        InterRefs?.CampBListLabel?.Text = "阵营 B:\n" + (_campBUnits.Count > 0 ? string.Join("\n", _campBUnits) : "(空)");
    }

    private void OnStartBattleClicked() {
        if (_campAUnits.Count == 0 && _campBUnits.Count == 0) {
            InterRefs?.StatusLabel?.Text = "请先为至少一个阵营添加单位！";
            return;
        }

        _logger.LogInformation("请求开始战斗: {RoomId}, campA={CampACount}, campB={CampBCount}", _roomId, _campAUnits.Count, _campBUnits.Count);

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

    private static string CampName(EnumCamp camp) => camp switch {
        EnumCamp.Camp_A => "阵营 A",
        EnumCamp.Camp_B => "阵营 B",
        _ => "未知",
    };

    private static string CategoryDisplayName(RoomCategory cat) => cat switch {
        RoomCategory.Casual => "休闲",
        RoomCategory.Competitive => "竞技",
        RoomCategory.Practice => "练习",
        RoomCategory.Tournament => "赛事",
        _ => "未知",
    };
}
