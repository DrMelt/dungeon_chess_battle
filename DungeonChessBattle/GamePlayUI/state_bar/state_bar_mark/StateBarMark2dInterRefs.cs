using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// StateBarMark2d 的导出引用集合，将 [Export] 字段从主脚本分离到独立节点。
/// </summary>
public partial class StateBarMark2dInterRefs : Node {
    /// <summary>单位血条状态组件。</summary>
    [Export]
    public HP_StateBar? PanelUnitStateBarRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验导出引用是否已赋值，缺失时打印错误日志。
    /// </summary>
    public override void _Ready() {
        if (PanelUnitStateBarRef == null)
            GD.PrintErr("[StateBarMark2dInterRefs] [Export] PanelUnitStateBarRef is not assigned!");
    }
}
