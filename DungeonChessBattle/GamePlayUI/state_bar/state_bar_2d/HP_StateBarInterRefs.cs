using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// HP_StateBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class HP_StateBarInterRefs : Node {
    /// <summary>玩家 UI 设置资源，用于获取阵营颜色。</summary>
    [Export]
    public PlayerUISettings? PlayerUISettingsRef {
        get; set;
    }

    /// <summary>血量进度条。</summary>
    [Export]
    public ProgressBar? ProgressBarRef {
        get; set;
    }

    /// <summary>生命/护盾百分比标签。</summary>
    [Export]
    public Label? LabelPercentRef {
        get; set;
    }

    /// <summary>生命/护盾数值标签。</summary>
    [Export]
    public Label? LabelCurrentValueRef {
        get; set;
    }

    /// <summary>单位名称标签。</summary>
    [Export]
    public Label? LabelObjectNameRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PlayerUISettingsRef == null)
            GD.PrintErr("[HP_StateBarInterRefs] [Export] PlayerUISettingsRef is not assigned!");
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
