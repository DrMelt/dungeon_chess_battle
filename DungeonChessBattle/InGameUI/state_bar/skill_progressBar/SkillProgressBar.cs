using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 技能施法进度条，展示当前正在施放的技能名称、剩余时间与进度。
/// </summary>
public partial class SkillProgressBar : Control, IUIUpdate {
    /// <summary>导出引用集合节点。</summary>
    public SkillProgressBarInterRefs? InterRefs {
        get; private set;
    }

    /// <summary>
    /// 节点就绪：获取引用集合节点。
    /// </summary>
    public override void _Ready() {
        InterRefs = GetNode<SkillProgressBarInterRefs>("SkillProgressBarInterRefs");
    }

    /// <summary>
    /// 根据单位施法状态刷新进度条；无施法时隐藏。
    /// </summary>
    /// <param name="unitShow">目标单位状态。</param>
    public void UpdateUI_WithUnit(UnitState unitShow) {
        if (InterRefs == null)
            return;
        var spellingSkill = unitShow.SpellingSkill;
        if (spellingSkill != null) {
            Visible = true;
            InterRefs.LabelSkillNameRef?.Text = spellingSkill.SkillName;
            InterRefs.LabelRemainingTimeRef?.Text = (spellingSkill.SkillSpellTime - spellingSkill.SkillSpelledTime).ToString("F1");
            InterRefs.ProgressBarRef?.Value = spellingSkill.SkillSpelledTime / spellingSkill.SkillSpellTime;
        }
        else {
            Visible = false;
        }
    }
}
