using Godot;

namespace DungeonChessBattle;

/// <summary>
/// SkillsList 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class SkillsListInterRefs : Node {
    /// <summary>技能按钮使用的场景资源。</summary>
    [Export]
    public PackedScene? SkillButtonPackedScene {
        get; private set;
    }
    /// <summary>技能按钮横向排列容器。</summary>
    [Export]
    public HBoxContainer? HBoxContainerRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (SkillButtonPackedScene == null)
            GD.PrintErr("[SkillsListInterRefs] [Export] SkillButtonPackedScene is not assigned!");
        if (HBoxContainerRef == null)
            GD.PrintErr("[SkillsListInterRefs] [Export] HBoxContainerRef is not assigned!");
    }
}
