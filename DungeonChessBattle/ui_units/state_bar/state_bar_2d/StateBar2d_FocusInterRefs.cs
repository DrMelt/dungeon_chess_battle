using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBar2d_Focus 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBar2d_FocusInterRefs : Node {
    [Export]
    public UserInterfaceRes? UserInterfaceRes { get; set; }

    [Export]
    public UserUISettings? UserUISettingsRef { get; set; }

    [Export]
    public ContainerBuffs? HboxContainerBuffsRef { get; set; }

    [Export]
    public HP_StateBar? PanelFocusStateRef { get; set; }

    [Export]
    public SkillProgressBar? PanelSkillProgressBarRef { get; set; }

    public override void _Ready() {
        if (UserInterfaceRes == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] UserInterfaceRes is not assigned!");
        if (UserUISettingsRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] UserUISettingsRef is not assigned!");
        if (HboxContainerBuffsRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] HboxContainerBuffsRef is not assigned!");
        if (PanelFocusStateRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] PanelFocusStateRef is not assigned!");
        if (PanelSkillProgressBarRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] PanelSkillProgressBarRef is not assigned!");
    }
}
