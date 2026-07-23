using DungeonChessBattle.Core;
using Godot;

namespace DungeonChessBattle {

    public partial class SkillProgressBar : Control, IUI_Update {
        [ExportGroup("References")]
        [Export]
        ProgressBar progressBarRef;
        [Export]
        Label label_SkillNameRef;
        [Export]
        Label label_RemainingTimeRef;

        public void UpdateUI_WithUnit(UnitState unitShow) {
            var spellingSkill = unitShow.SpellingSkill;
            if (spellingSkill != null) {
                Visible = true;
                label_SkillNameRef.Text = spellingSkill.SkillName;
                label_RemainingTimeRef.Text = (spellingSkill.SkillSpellTime - spellingSkill.SkillSpelledTime).ToString("F1");
                progressBarRef.Value = spellingSkill.SkillSpelledTime / spellingSkill.SkillSpellTime;
            }
            else {
                Visible = false;
            }
        }
    }

}
