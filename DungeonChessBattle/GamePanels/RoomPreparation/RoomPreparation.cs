using Godot;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Services;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle;

/// <summary>
/// 房间准备界面。玩家进入房间后选择阵营单位，准备就绪后开始战斗。
/// 通过连接 GameLobby.RoomEntered 信号自动切入准备流程。
/// </summary>
public partial class RoomPreparation : BaseGamePanel {
    private readonly ILogger<RoomPreparation> _logger = ServiceLocator.GetLogger<RoomPreparation>();

    [Signal]
    public delegate void BattleStartRequestedEventHandler(string roomId);

    #region Service & State

    private IClientBattleService? _clientService;

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

        SelectCamp(EnumCamp.Camp_A);
        PopulateUnitCards();
        _logger.LogInformation("RoomPreparation ready");
    }

    /// <summary>
    /// 由 GameLobby 调用，设置房间信息并准备就绪。
    /// </summary>
    public void EnterRoom(string roomId, IClientBattleService? service) {
        _roomId = roomId;
        _logger.LogInformation("进入房间: {RoomId}, service={ServiceType}", roomId, service?.GetType().Name);

        // 取消旧服务的订阅
        if (_clientService != null) {
            _clientService.OnUnitCreated -= OnServiceUnitCreated;
            _clientService.BattlePhaseChanged -= OnServiceBattlePhase;
        }

        _clientService = service;

        // 订阅新服务的事件
        if (service != null) {
            service.OnUnitCreated += OnServiceUnitCreated;
            service.BattlePhaseChanged += OnServiceBattlePhase;
        }

        InterRefs?.RoomNameLabel?.Text = $"房间: {roomId}";
        InterRefs?.StatusLabel?.Text = "请选择单位...";
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
        if (string.IsNullOrEmpty(_selectedUnitKey) || _clientService == null)
            return;

        string displayName = AvailableUnits[_selectedUnitKey].displayName;
        byte camp = _selectedCamp == EnumCamp.Camp_A ? (byte)1 : (byte)2;

        // 网络模式：等待 OnServiceUnitCreated 回调确认
        // 本地模式：回调同步触发，幂等过滤
        _clientService.CreateUnit(_roomId, displayName, camp);

        InterRefs?.StatusLabel?.Text = $"请求创建 {displayName} ({CampName(_selectedCamp)})...";
    }

    private void OnServiceUnitCreated(string eventRoomId, string unitName, byte camp) {
        if (eventRoomId != _roomId)
            return;

        _logger.LogInformation("单位已创建: {UnitName}, camp={Camp}, room={RoomId}", unitName, camp, eventRoomId);

        if (camp == 1) {
            if (!_campAUnits.Contains(unitName))
                _campAUnits.Add(unitName);
        }
        else if (camp == 2) {
            if (!_campBUnits.Contains(unitName))
                _campBUnits.Add(unitName);
        }

        UpdateCampLists();
        InterRefs?.StartBattleButton?.Disabled = _campAUnits.Count == 0 && _campBUnits.Count == 0;
        InterRefs?.StatusLabel?.Text = $"{unitName} 已加入 {(camp == 1 ? "阵营 A" : "阵营 B")}";
    }

    private void OnServiceBattlePhase(string eventRoomId, BattlePhase phase) {
        if (eventRoomId != _roomId)
            return;
        GD.Print($"[RoomPreparation] Battle phase changed: {phase}");
    }

    private void UpdateCampLists() {
        InterRefs?.CampAListLabel?.Text = "阵营 A:\n" + string.Join("\n", _campAUnits);
        InterRefs?.CampBListLabel?.Text = "阵营 B:\n" + string.Join("\n", _campBUnits);
    }

    private void OnStartBattleClicked() {
        if (_campAUnits.Count == 0 && _campBUnits.Count == 0) {
            InterRefs?.StatusLabel?.Text = "请先为至少一个阵营添加单位！";
            return;
        }

        _logger.LogInformation("请求开始战斗: {RoomId}, campA={CampACount}, campB={CampBCount}", _roomId, _campAUnits.Count, _campBUnits.Count);
        Visible = false;
        EmitSignal(SignalName.BattleStartRequested, _roomId);
    }

    private static string CampName(EnumCamp camp) => camp switch {
        EnumCamp.Camp_A => "阵营 A",
        EnumCamp.Camp_B => "阵营 B",
        _ => "未知",
    };
}
