using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBarMini 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarMiniInterRefs : Node {
    /// <summary>Buff 图标容器组件。</summary>
    [Export]
    public ContainerBuffs? ContainerBuffsRef {
        get; set;
    }

    /// <summary>悬停高亮外框。</summary>
    [Export]
    public Panel? OutlineRef {
        get; set;
    }

    /// <summary>血条状态组件。</summary>
    [Export]
    public HP_StateBar? HpStateBarRef {
        get; set;
    }

    /// <summary>施法进度条组件。</summary>
    [Export]
    public SkillProgressBar? SkillProgressBarRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (ContainerBuffsRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] ContainerBuffsRef is not assigned!");
        if (OutlineRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] OutlineRef is not assigned!");
        if (HpStateBarRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] HpStateBarRef is not assigned!");
        if (SkillProgressBarRef == null)
            GD.PrintErr("[StateBarMiniInterRefs] [Export] SkillProgressBarRef is not assigned!");
    }
}
