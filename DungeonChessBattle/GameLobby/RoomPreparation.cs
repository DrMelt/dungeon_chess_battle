using Godot;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle;

/// <summary>
/// 房间准备界面。玩家进入房间后选择阵营单位，准备就绪后开始战斗。
/// 通过连接 GameLobby.RoomEntered 信号自动切入准备流程。
/// </summary>
public partial class RoomPreparation : Control {
    [Signal]
    public delegate void BattleStartRequestedEventHandler(string roomId);

    #region Exported Node References

    [Export] private Label _roomNameLabel = null!;
    [Export] private Label _statusLabel = null!;
    [Export] private GridContainer _unitCardGrid = null!;
    [Export] private Button _campAButton = null!;
    [Export] private Button _campBButton = null!;
    [Export] private Button _startBattleButton = null!;
    [Export] private Label _campAListLabel = null!;
    [Export] private Label _campBListLabel = null!;
    [Export] private PackedScene _unitCardScene = null!;

    #endregion

    #region Service & State

    private GameLobby? _lobby;
    private string _roomId = "";
    private EnumCamp _selectedCamp = EnumCamp.Camp_A;
    private string? _selectedUnitKey;
    private System.Collections.Generic.List<string> _campAUnits = [];
    private System.Collections.Generic.List<string> _campBUnits = [];

    // 可用单位配置（configKey → displayName & unitConfig）
    private static readonly System.Collections.Generic.Dictionary<string, (string displayName, UnitConfig config)> AvailableUnits = new() {
        ["WhiteMage"] = ("White Mage", GameConfigDB.UnitWhiteMage),
    };

    #endregion

    public override void _Ready() {
        // 自动查找同级 GameLobby 节点并绑定信号
        _lobby = GetParent().GetNode<GameLobby>("GameLobby");
        _lobby.RoomEntered += OnRoomEntered;
        BattleStartRequested += _lobby.StartBattle;

        _campAButton.Pressed += () => SelectCamp(EnumCamp.Camp_A);
        _campBButton.Pressed += () => SelectCamp(EnumCamp.Camp_B);
        _startBattleButton.Pressed += OnStartBattleClicked;
        _startBattleButton.Disabled = true;

        SelectCamp(EnumCamp.Camp_A);
        PopulateUnitCards();
    }

    private void OnRoomEntered(string roomId) {
        _roomId = roomId;
        _roomNameLabel.Text = $"房间: {roomId}";
        _statusLabel.Text = "请选择单位...";
        Visible = true;

        // 隐藏大厅
        _lobby!.Visible = false;
    }

    private void PopulateUnitCards() {
        foreach (Node child in _unitCardGrid.GetChildren())
            child.QueueFree();

        foreach (var (key, (displayName, config)) in AvailableUnits) {
            var card = _unitCardScene.Instantiate<UnitSelectCard>();
            string stats = $"HP: {config.MaxHealth:F0}  SPD: {config.BaseSpeed:F1}";
            card.Setup(key, displayName, stats);
            card.UnitSelected += OnUnitSelected;
            _unitCardGrid.AddChild(card);
        }
    }

    private void OnUnitSelected(string unitConfigKey) {
        _selectedUnitKey = unitConfigKey;
        _statusLabel.Text = $"已选择: {AvailableUnits[unitConfigKey].displayName} (将加入 {CampName(_selectedCamp)})";
        AddUnitToCamp();
    }

    private void SelectCamp(EnumCamp camp) {
        _selectedCamp = camp;
        _campAButton.ButtonPressed = camp == EnumCamp.Camp_A;
        _campBButton.ButtonPressed = camp == EnumCamp.Camp_B;
        _statusLabel.Text = $"当前阵营: {CampName(camp)} - 点击单位卡片添加";
    }

    private void AddUnitToCamp() {
        if (string.IsNullOrEmpty(_selectedUnitKey) || _lobby?.ClientService == null)
            return;

        string displayName = AvailableUnits[_selectedUnitKey].displayName;

        if (_selectedCamp == EnumCamp.Camp_A) {
            if (!_campAUnits.Contains(displayName)) {
                _campAUnits.Add(displayName);
                _lobby.ClientService.CreateUnit(_roomId, displayName, 1);
            }
        }
        else {
            if (!_campBUnits.Contains(displayName)) {
                _campBUnits.Add(displayName);
                _lobby.ClientService.CreateUnit(_roomId, displayName, 2);
            }
        }

        UpdateCampLists();
        _startBattleButton.Disabled = _campAUnits.Count == 0 && _campBUnits.Count == 0;
        _statusLabel.Text = $"{displayName} 已加入 {CampName(_selectedCamp)}";
    }

    private void UpdateCampLists() {
        _campAListLabel.Text = "阵营 A:\n" + string.Join("\n", _campAUnits);
        _campBListLabel.Text = "阵营 B:\n" + string.Join("\n", _campBUnits);
    }

    private void OnStartBattleClicked() {
        if (_campAUnits.Count == 0 && _campBUnits.Count == 0) {
            _statusLabel.Text = "请先为至少一个阵营添加单位！";
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