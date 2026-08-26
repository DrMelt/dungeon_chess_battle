using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.Game.GamePlayUI;

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
    /// <param name="unit">目标单位展示视图。</param>
    /// <param name="session">战斗会话上下文，向下传递给血条做阵营关系着色。</param>
    public void UpdateUI_WithUnit(IUnitUiView unit, BattleSessionContext? session) {
        var camera3D = GetViewport().GetCamera3D();
        var pos = unit.Position;
        var screenPos = camera3D.UnprojectPosition(new Vector3(pos.X, 0f, pos.Y) + Vector3.Up * 2.2f);
        GlobalPosition = screenPos;

        InterRefs?.PanelUnitStateBarRef?.UpdateUI_WithUnit(unit, session);
    }
}
