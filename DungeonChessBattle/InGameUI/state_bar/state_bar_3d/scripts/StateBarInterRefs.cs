using Godot;

namespace DungeonChessBattle;

/// <summary>
/// StateBar 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarInterRefs : Node {
    /// <summary>玩家 UI 设置资源，用于获取阵营颜色。</summary>
    [Export]
    public PlayerUISettings? PlayerUISettingsRef {
        get; set;
    }

    /// <summary>血条网格实例。</summary>
    [Export]
    public MeshInstance3D? StateBarRef {
        get; set;
    }

    /// <summary>生命/护盾百分比 3D 标签。</summary>
    [Export]
    public Label3D? Label3DPercentRef {
        get; set;
    }

    /// <summary>生命/护盾数值 3D 标签。</summary>
    [Export]
    public Label3D? Label3DCurrentValueRef {
        get; set;
    }

    /// <summary>单位名称 3D 标签。</summary>
    [Export]
    public Label3D? Label3DNameRef {
        get; set;
    }

    /// <summary>
    /// 节点就绪时校验所有导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PlayerUISettingsRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] PlayerUISettingsRef is not assigned!");
        if (StateBarRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] StateBarRef is not assigned!");
        if (Label3DPercentRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] Label3DPercentRef is not assigned!");
        if (Label3DCurrentValueRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] Label3DCurrentValueRef is not assigned!");
        if (Label3DNameRef == null)
            GD.PrintErr("[StateBarInterRefs] [Export] Label3DNameRef is not assigned!");
    }
}
