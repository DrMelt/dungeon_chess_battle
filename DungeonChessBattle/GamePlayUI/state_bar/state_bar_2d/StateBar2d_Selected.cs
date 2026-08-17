using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.GamePlayUI;

/// <summary>
/// 焦点/选中单位的 2D 状态栏容器。
/// 每帧按本地焦点单位刷新显示 Buff、状态与施法进度，无选中时隐藏。
/// </summary>
public partial class StateBar2d_Selected : Control {

    /// <summary>战斗会话上下文引用，提供本地焦点单位 Pawn 并向下传递给血条做阵营关系着色。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

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
        var focusPawn = _sessionRef?.LocalFocusPawn;

        if (focusPawn != null) {
            Visible = true;

            InterRefs.HboxContainerBuffsRef?.UpdateUI_WithUnit(focusPawn);
            InterRefs.PanelFocusStateRef?.UpdateUI_WithUnit(focusPawn, _sessionRef);
            InterRefs.PanelSkillProgressBarRef?.UpdateUI_WithUnit(focusPawn);
        }
        else {
            Visible = false;
        }
    }
}
