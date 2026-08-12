using Godot;
using System;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 单位交互区域组件，用于捕获鼠标点击/悬停事件并关联所属单位。
/// </summary>
public partial class UnitShowArea3D : Area3D {
    /// <summary>所属单位展示组件引用。</summary>
    [Export]
    private UnitGameShow? unitShowRef;
    /// <summary>所属单位展示组件。</summary>
    public UnitGameShow UnitShowRef => unitShowRef ?? throw new InvalidOperationException("UnitShowRef has not been assigned.");

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (unitShowRef == null)
            GD.PrintErr("[UnitShowArea3D] [Export] unitShowRef is not assigned!");
    }
}
