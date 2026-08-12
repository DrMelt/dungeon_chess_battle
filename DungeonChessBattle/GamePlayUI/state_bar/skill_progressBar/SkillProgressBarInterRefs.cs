using Godot;

namespace DungeonChessBattle;

/// <summary>
/// SkillProgressBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class SkillProgressBarInterRefs : Node {
    /// <summary>施法进度条。</summary>
    [Export]
    public ProgressBar? ProgressBarRef {
        get; set;
    }

    /// <summary>技能名称标签。</summary>
    [Export]
    public Label? LabelSkillNameRef {
        get; set;
    }

    /// <summary>剩余施法时间标签。</summary>
    [Export]
    public Label? LabelRemainingTimeRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ProgressBarRef == null)
            GD.PrintErr("[SkillProgressBarInterRefs] [Export] ProgressBarRef is not assigned!");
        if (LabelSkillNameRef == null)
            GD.PrintErr("[SkillProgressBarInterRefs] [Export] LabelSkillNameRef is not assigned!");
        if (LabelRemainingTimeRef == null)
            GD.PrintErr("[SkillProgressBarInterRefs] [Export] LabelRemainingTimeRef is not assigned!");
    }
}
