using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBar2d_Focus 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBar2d_FocusInterRefs : Node {
    /// <summary>玩家界面资源，提供焦点/悬停单位数据。</summary>
    [Export]
    public PlayerInterfaceRes? PlayerInterfaceRes {
        get; set;
    }

    /// <summary>玩家 UI 设置资源。</summary>
    [Export]
    public PlayerUISettings? PlayerUISettingsRef {
        get; set;
    }

    /// <summary>Buff 图标容器组件。</summary>
    [Export]
    public ContainerBuffs? HboxContainerBuffsRef {
        get; set;
    }

    /// <summary>血条状态组件。</summary>
    [Export]
    public HP_StateBar? PanelFocusStateRef {
        get; set;
    }

    /// <summary>施法进度条组件。</summary>
    [Export]
    public SkillProgressBar? PanelSkillProgressBarRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PlayerInterfaceRes == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] PlayerInterfaceRes is not assigned!");
        if (PlayerUISettingsRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] PlayerUISettingsRef is not assigned!");
        if (HboxContainerBuffsRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] HboxContainerBuffsRef is not assigned!");
        if (PanelFocusStateRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] PanelFocusStateRef is not assigned!");
        if (PanelSkillProgressBarRef == null)
            GD.PrintErr("[StateBar2d_FocusInterRefs] [Export] PanelSkillProgressBarRef is not assigned!");
    }
}
