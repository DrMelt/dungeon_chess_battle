using Godot;

namespace DungeonChessBattle;

/// <summary>
/// HP_StateBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class HP_StateBarInterRefs : Node {
    [Export]
    public UserUISettings? UserUISettingsRef {
        get; set;
    }

    [Export]
    public ProgressBar? ProgressBarRef {
        get; set;
    }

    [Export]
    public Label? LabelPercentRef {
        get; set;
    }

    [Export]
    public Label? LabelCurrentValueRef {
        get; set;
    }

    [Export]
    public Label? LabelObjectNameRef {
        get; set;
    }

    public override void _Ready() {
        if (UserUISettingsRef == null)
            GD.PrintErr("[HP_StateBarInterRefs] [Export] UserUISettingsRef is not assigned!");
        if (ProgressBarRef == null)
            GD.PrintErr("[HP_StateBarInterRefs] [Export] ProgressBarRef is not assigned!");
        if (LabelPercentRef == null)
            GD.PrintErr("[HP_StateBarInterRefs] [Export] LabelPercentRef is not assigned!");
        if (LabelCurrentValueRef == null)
            GD.PrintErr("[HP_StateBarInterRefs] [Export] LabelCurrentValueRef is not assigned!");
        if (LabelObjectNameRef == null)
            GD.PrintErr("[HP_StateBarInterRefs] [Export] LabelObjectNameRef is not assigned!");
    }
}
