using Godot;
using System;
using System.Collections.Generic;


namespace GameLogic
{
    public partial class Skill_CircularRange_Damage : UnitSkillBase
    {
        public enum Enum_DamageType
        {
            Physcial,
            Magic,

        }

        [Export]
        float damage = 0;
        [Export]
        Enum_DamageType enum_DamageType;

        [Export]
        float nearClamp = 1.0f;
        [Export]
        float farClamp = 1.0f;
        [Export]
        float radianFrom = -1.0f;
        [Export]
        float radianTo = 1.0f;


        public override void CallSkill(UnitState callObject, UnitState targetObject, Vector3? targetPos, List<UnitState> skillObjects)
        {
            foreach (UnitState skillObject in skillObjects)
            {
                Utility.IsInRange_Circular(
                    skillObject.Position,
                    callObject.Position,
                    (Vector3)(targetPos - skillObject.Position),
                    nearClamp,
                    farClamp,
                    radianFrom,
                    radianTo);


            }
        }

    }

}
