using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 单个单位的 2D 状态标记，投影单位位置到屏幕并同步刷新状态条。
/// </summary>
public partial class StateBarMark2d : Control, IUIUpdate {
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
    /// <param name="unitState">目标单位状态。</param>
    public void UpdateUI_WithUnit(UnitState unitState) {
        var camera3D = GetViewport().GetCamera3D();
        var screenPos = camera3D.UnprojectPosition(unitState.Position + Vector3.Up * 2.2f);
        GlobalPosition = screenPos;

        InterRefs?.PanelUnitStateBarRef?.UpdateUI_WithUnit(unitState);
    }
}
