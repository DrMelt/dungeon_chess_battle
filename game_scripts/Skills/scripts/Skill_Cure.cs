using Godot;
using System;
using System.Collections.Generic;


namespace GameLogic {
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
