using Godot;

namespace DungeonChessBattle;

/// <summary>
/// SkillProgressBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class SkillProgressBarInterRefs : Node {
    [Export]
    public ProgressBar? ProgressBarRef { get; set; }

    [Export]
    public Label? LabelSkillNameRef { get; set; }

    [Export]
    public Label? LabelRemainingTimeRef { get; set; }

    public override void _Ready() {
        if (ProgressBarRef == null)
            GD.PrintErr("[SkillProgressBarInterRefs] [Export] ProgressBarRef is not assigned!");
        if (LabelSkillNameRef == null)
            GD.PrintErr("[SkillProgressBarInterRefs] [Export] LabelSkillNameRef is not assigned!");
        if (LabelRemainingTimeRef == null)
            GD.PrintErr("[SkillProgressBarInterRefs] [Export] LabelRemainingTimeRef is not assigned!");
    }
}
