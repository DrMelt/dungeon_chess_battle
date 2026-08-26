using DungeonChessBattle.MainScene;
using Godot;

namespace DungeonChessBattle.Game.GamePlayUI;

/// <summary>
/// 玩家控制单位的 2D 状态栏容器。
/// 每帧按本地玩家单位刷新显示 Buff、状态与施法进度，无本地单位时隐藏。
/// </summary>
public partial class StateBar2d_LocalPlayer : Control {

    /// <summary>战斗会话上下文引用，提供本地玩家单位 Pawn 并向下传递给血条做阵营关系着色。</summary>
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
    /// 每帧检查本地玩家单位，无单位则隐藏，否则显示并刷新子组件。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (InterRefs == null)
            return;
        var localUnit = _sessionRef?.LocalUnit;

        if (localUnit != null) {
            Visible = true;

            InterRefs.HboxContainerBuffsRef?.UpdateUI_WithUnit(localUnit);
            InterRefs.PanelFocusStateRef?.UpdateUI_WithUnit(localUnit, _sessionRef);
            InterRefs.PanelSkillProgressBarRef?.UpdateUI_WithUnit(localUnit);
        }
        else {
            Visible = false;
        }
    }
}
