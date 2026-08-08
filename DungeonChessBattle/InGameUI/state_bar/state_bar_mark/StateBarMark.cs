using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 状态标记容器，为场景中所有单位生成对应的 2D 状态标记。
/// </summary>
public partial class StateBarMark : Control {
    /// <summary>战斗单位管理器引用。</summary>
    [Export]
    private BattleUnitManager? unitsInScene_Show_Ref;
    /// <summary>2D 状态标记使用的场景资源。</summary>
    [Export]
    private PackedScene? stateBarSimple2d_PKD;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (unitsInScene_Show_Ref == null)
            GD.PrintErr("[StateBarMark] [Export] unitsInScene_Show_Ref is not assigned!");
        if (stateBarSimple2d_PKD == null)
            GD.PrintErr("[StateBarMark] [Export] stateBarSimple2d_PKD is not assigned!");
    }

    /// <summary>
    /// 每帧清空并重建所有单位的状态标记。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (unitsInScene_Show_Ref == null || stateBarSimple2d_PKD == null)
            return;

        var children = GetChildren();
        foreach (var child in children) {
            child.QueueFree();
        }


        Godot.Collections.Array<UnitState> units = unitsInScene_Show_Ref.UnitsArr;
        foreach (UnitState unit in units) {
            var stateBarSimple2d = stateBarSimple2d_PKD.Instantiate<StateBarMark2d>();
            AddChild(stateBarSimple2d);
            stateBarSimple2d.UpdateUI_WithUnit(unit);
        }


    }
}
