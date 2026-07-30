using Godot;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle;

/// <summary>
/// 房间准备界面。玩家进入房间后选择阵营单位，准备就绪后开始战斗。
/// 通过连接 GameLobby.RoomEntered 信号自动切入准备流程。
/// </summary>
public partial class RoomPreparation : BaseGamePanel {
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
        InterRefs?.BackButton?.Pressed += ClosePanel;
        var startBtn = InterRefs?.StartBattleButton;
        if (startBtn is not null) {
            startBtn.Pressed += OnStartBattleClicked;
            startBtn.Disabled = true;
        }

        SelectCamp(EnumCamp.Camp_A);
        PopulateUnitCards();
    }

    /// <summary>
    /// 由 GameLobby 调用，设置房间信息并准备就绪。
    /// </summary>
    public void EnterRoom(string roomId, IClientBattleService? service) {
        _roomId = roomId;
        _clientService = service;
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

        if (_selectedCamp == EnumCamp.Camp_A) {
            if (!_campAUnits.Contains(displayName)) {
                _campAUnits.Add(displayName);
                _clientService.CreateUnit(_roomId, displayName, 1);
            }
        }
        else {
            if (!_campBUnits.Contains(displayName)) {
                _campBUnits.Add(displayName);
                _clientService.CreateUnit(_roomId, displayName, 2);
            }
        }

        UpdateCampLists();
        InterRefs?.StartBattleButton?.Disabled = _campAUnits.Count == 0 && _campBUnits.Count == 0;
        InterRefs?.StatusLabel?.Text = $"{displayName} 已加入 {CampName(_selectedCamp)}";
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

        Visible = false;
        EmitSignal(SignalName.BattleStartRequested, _roomId);
    }

    private static string CampName(EnumCamp camp) => camp switch {
        EnumCamp.Camp_A => "阵营 A",
        EnumCamp.Camp_B => "阵营 B",
        _ => "未知",
    };
}
