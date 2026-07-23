using Godot;
using DungeonChessBattle.Core.Skills;


namespace DungeonChessBattle.Core {
    [GlobalClass]
    public partial class Skill_Cure : UnitSkillBase {
        [Export]
        float curePotency = 0;

        protected override void CallSpelledSkill() {
            float health = callSkillObject.CureAmount(curePotency);
            targetObject.RestoreHealth(health);
        }
    }
}
