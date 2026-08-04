using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class SkillProgressBar : Control, IUI_Update {
    public SkillProgressBarInterRefs? InterRefs {
        get; private set;
    }

    public override void _Ready() {
        InterRefs = GetNode<SkillProgressBarInterRefs>("SkillProgressBarInterRefs");
    }

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
