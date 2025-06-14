using Godot;
using System;
using System.Collections.Generic;


namespace GameLogic {
    [GlobalClass]
    public partial class Skill_Damage : UnitSkillBase {
        public enum Enum_DamageType {
            Physcial,
            Magic,
        }

        [Export]
        float damage = 0;
        [Export]
        Enum_DamageType enum_DamageType;

        public override void CallSkill(UnitState callObject, UnitState targetObject, Vector3? targetPos, List<UnitState> skillObjects) {
            if (enum_DamageType == Enum_DamageType.Physcial) {
                float damageAmount = callObject.PhysicalDamageAmount(damage);
                targetObject.TakeDamage(damageAmount, 0);
            }
            else if (enum_DamageType == Enum_DamageType.Magic) {
                float damageAmount = callObject.MagicDamageAmount(damage);
                targetObject.TakeDamage(0, damageAmount);
            }
        }
    }
}
