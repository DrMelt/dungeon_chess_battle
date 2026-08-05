using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 圆形范围技能提示的导出引用集合节点。
/// </summary>
public partial class SkillRangeCircular_HintInterRefs : Node {
    /// <summary>
    /// 范围贴花网格实例引用。
    /// </summary>
    [Export]
    public MeshInstance3D? DecalRef {
        get; private set;
    }

    /// <summary>
    /// 节点就绪时校验关键导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (DecalRef == null) {
            GD.PrintErr("[SkillRangeCircular_HintInterRefs] [Export] DecalRef is not assigned!");
        }
    }
}
