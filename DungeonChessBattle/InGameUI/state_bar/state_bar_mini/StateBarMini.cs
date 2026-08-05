using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 迷你状态条组件，展示单个单位的 Buff、血条与施法进度，支持鼠标悬停高亮外框。
/// </summary>
public partial class StateBarMini : Control {

    /// <summary>导出引用集合节点。</summary>
    public StateBarMiniInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>鼠标是否悬停在该状态条上。</summary>
    private bool mouseOn = false;

    /// <summary>当前绑定的单位状态。</summary>
    private UnitState? bindingUnitStateRes;

    /// <summary>
    /// 节点就绪：获取引用集合，并监听鼠标悬停以显示/隐藏外框。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<StateBarMiniInterRefs>("StateBarMiniInterRefs");
        MouseEntered += () => {
            mouseOn = true;
            if (InterRefs?.OutlineRef != null)
                InterRefs.OutlineRef.Visible = true;
        };
        MouseExited += () => {
            mouseOn = false;
            if (InterRefs?.OutlineRef != null)
                InterRefs.OutlineRef.Visible = false;
        };
    }

    /// <summary>
    /// 绑定要展示的单位状态。
    /// </summary>
    /// <param name="unitState">目标单位状态。</param>
    public void BindUnitState(UnitState unitState) {
        bindingUnitStateRes = unitState;
    }

    /// <summary>
    /// 每帧刷新子组件的单位状态展示。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (InterRefs == null || bindingUnitStateRes == null)
            return;
        InterRefs.ContainerBuffsRef?.UpdateUI_WithUnit(bindingUnitStateRes);
        InterRefs.HpStateBarRef?.UpdateUI_WithUnit(bindingUnitStateRes);
        InterRefs.SkillProgressBarRef?.UpdateUI_WithUnit(bindingUnitStateRes);
    }


}
