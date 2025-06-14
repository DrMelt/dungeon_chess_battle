using Godot;
using System;
using System.Collections.Generic;


namespace GameLogic
{
    [GlobalClass]
    public partial class Skill_Cure : UnitSkillBase
    {
        [Export]
        float curePotency = 0;

        public override void CallSkill(UnitState callObject, UnitState targetObject, Vector3? targetPos, List<UnitState> skillObjects)
        {
            float health = callObject.CureAmount(curePotency);
            targetObject.RestoreHealth(health);
        }
    }
}
