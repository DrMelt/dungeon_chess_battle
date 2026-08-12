using DungeonChessBattle.GameAssets;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 焦点/悬停单位的 2D 状态栏容器。
/// 每帧根据鼠标悬停或焦点单位刷新显示 Buff、状态与施法进度。
/// </summary>
public partial class StateBar2d_Focus : Control {

    /// <summary>导出引用集合节点。</summary>
    public StateBar2d_FocusInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateBar2d_FocusInterRefs>("StateBar2d_FocusInterRefs");
    }

    /// <summary>
    /// 每帧检查鼠标悬停/焦点单位，有则显示状态栏并刷新子组件，无则隐藏。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (!Engine.IsEditorHint()) {
            Visible = false;
            if (InterRefs == null)
                return;
            UnitGameShow? showUnit = GetUnitShow();

            if (showUnit != null) {
                Visible = true;
                var pawn = showUnit.Pawn;

                InterRefs.HboxContainerBuffsRef?.UpdateUI_WithUnit(pawn);
                InterRefs.PanelFocusStateRef?.UpdateUI_WithUnit(pawn);
                InterRefs.PanelSkillProgressBarRef?.UpdateUI_WithUnit(pawn);
            }

        }
    }

    /// <summary>
    /// 获取需要展示的单位：优先鼠标悬停单位，其次焦点单位。
    /// </summary>
    /// <returns>要展示的单位，无则为 null。</returns>
    private UnitGameShow? GetUnitShow() {
        UnitGameShow? showUnit = null;
        var uiRes = InterRefs?.PlayerInterfaceRes;
        if (uiRes == null)
            return null;
        UnitGameShow? mouseOnUnit = uiRes.MouseOnUnit;
        UnitGameShow? focusOnUnit = uiRes.FocusOnUnit;
        if (mouseOnUnit != null) {
            showUnit = mouseOnUnit;
        }
        else if (focusOnUnit != null) {
            showUnit = focusOnUnit;
        }
        return showUnit;
    }
}
