using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 焦点/选中单位的 2D 状态栏容器。
/// 每帧按本地焦点单位刷新显示 Buff、状态与施法进度，无选中时隐藏。
/// </summary>
public partial class StateBar2d_Selected : Control {

    /// <summary>战斗单位管理器引用，提供本地焦点单位视图。</summary>
    [Export]
    private BattleUnitManager? _unitManagerRef;

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
    /// 每帧检查选中单位，无选中则隐藏，否则显示并刷新子组件。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (InterRefs == null)
            return;
        var showUnit = _unitManagerRef?.LocalFocusUnit;

        if (showUnit != null) {
            Visible = true;
            var pawn = showUnit.Pawn;

            InterRefs.HboxContainerBuffsRef?.UpdateUI_WithUnit(pawn);
            InterRefs.PanelFocusStateRef?.UpdateUI_WithUnit(pawn);
            InterRefs.PanelSkillProgressBarRef?.UpdateUI_WithUnit(pawn);
        }
        else {
            Visible = false;
        }
    }
}
