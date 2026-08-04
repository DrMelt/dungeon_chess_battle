using Godot;

namespace DungeonChessBattle;

/// <summary>
/// UnitSelectPanel 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitSelectPanelInterRefs : Node {
    [Export]
    public Label? TitleLabel {
        get; private set;
    }
    [Export]
    public GridContainer? UnitCardGrid {
        get; private set;
    }
    [Export]
    public Button? BackButton {
        get; private set;
    }
    [Export]
    public PackedScene? UnitCardScene {
        get; private set;
    }

    public override void _Ready() {
        if (TitleLabel == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] TitleLabel is not assigned!");
        if (UnitCardGrid == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] UnitCardGrid is not assigned!");
        if (BackButton == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] BackButton is not assigned!");
        if (UnitCardScene == null)
            GD.PrintErr("[UnitSelectPanelInterRefs] [Export] UnitCardScene is not assigned!");
    }
}
