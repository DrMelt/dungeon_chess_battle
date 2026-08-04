using Godot;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle;

/// <summary>
/// 角色选取面板。以网格形式展示所有可用单位，用户点击选择一个职业。
/// 选择后发出 UnitSelected 信号并自动返回 RoomPreparation。
/// </summary>
public partial class UnitSelectPanel : BaseGamePanel {
    [Signal]
    public delegate void UnitSelectedEventHandler(string unitConfigKey);

    private UnitSelectPanelInterRefs? _refs;

    /// <summary>
    /// 可用单位配置（configKey → displayName, UnitConfig）。
    /// </summary>
    private static readonly System.Collections.Generic.Dictionary<string, (string DisplayName, UnitConfig Config)> AvailableUnits = new() {
        ["WhiteMage"] = ("White Mage", GameConfigDB.UnitWhiteMage),
    };

    public override void _Ready() {
        _refs = GetNode<UnitSelectPanelInterRefs>("UnitSelectPanelInterRefs");
        if (_refs is null) {
            GD.PrintErr("[UnitSelectPanel] UnitSelectPanelInterRefs node not found.");
            return;
        }

        _refs.BackButton?.Pressed += GoBack;
    }

    protected override void OnPanelOpened() {
        PopulateUnitGrid();
    }

    /// <summary>
    /// 填充可用单位网格。每次打开面板时重新创建 UnitCard。
    /// </summary>
    private void PopulateUnitGrid() {
        if (_refs?.UnitCardGrid is null || _refs?.UnitCardScene is null)
            return;

        // 清空旧卡片
        foreach (Node child in _refs.UnitCardGrid.GetChildren())
            child.QueueFree();

        foreach (var (key, (displayName, config)) in AvailableUnits) {
            var card = _refs.UnitCardScene.Instantiate<UnitCard>();
            string stats = $"HP: {config.MaxHealth:F0}  SPD: {config.BaseSpeed:F1}";
            card.Setup(key, displayName, stats);
            card.UnitSelected += OnCardSelected;
            _refs.UnitCardGrid.AddChild(card);
        }
    }

    private void OnCardSelected(string unitConfigKey) {
        EmitSignal(SignalName.UnitSelected, unitConfigKey);
        ClosePanel();
    }
}
