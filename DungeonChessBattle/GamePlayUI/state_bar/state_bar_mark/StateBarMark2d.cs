using DungeonChessBattle.Entities;
using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 单个单位的 2D 状态标记，投影单位位置到屏幕并同步刷新状态条。
/// </summary>
public partial class StateBarMark2d : Control {
    /// <summary>导出引用集合节点。</summary>
    public StateBarMark2dInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateBarMark2dInterRefs>("StateBarMark2dInterRefs");
    }

    /// <summary>
    /// 将单位头顶位置投影到屏幕坐标，并刷新状态条显示。
    /// </summary>
    /// <param name="pawn">目标单位 Pawn。</param>
    /// <param name="manager">战斗单位管理器，向下传递给血条做阵营关系着色。</param>
    public void UpdateUI_WithUnit(UnitPawn pawn, BattleUnitManager? manager) {
        var camera3D = GetViewport().GetCamera3D();
        var pos = pawn.Position.InterpolatedValue;
        var screenPos = camera3D.UnprojectPosition(new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
        GlobalPosition = screenPos;

        InterRefs?.PanelUnitStateBarRef?.UpdateUI_WithUnit(pawn, manager);
    }
}
