using Godot;

namespace DungeonChessBattle;

/// <summary>
/// UnitCard 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class UnitCardInterRefs : Node {
    [Export]
    public Label? NameLabel {
        get; private set;
    }
    [Export]
    public Label? UserNameLabel {
        get; private set;
    }
    [Export]
    public Label? HpLabel {
        get; private set;
    }
    [Export]
    public Label? HpValueLabel {
        get; private set;
    }
    [Export]
    public Panel? BgPanel {
        get; private set;
    }

    public override void _Ready() {
        if (NameLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] NameLabel is not assigned!");
        if (UserNameLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] UserNameLabel is not assigned!");
        if (HpLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] HpLabel is not assigned!");
        if (HpValueLabel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] HpValueLabel is not assigned!");
        if (BgPanel == null)
            GD.PrintErr("[UnitCardInterRefs] [Export] BgPanel is not assigned!");
    }
}
