using System;
using System.Collections.Generic;
using Godot;

namespace GameLogic {
    [GlobalClass]
    public partial class Skill_Range_Damage : UnitSkillBase {

        [Export]
        float damage = 0;

        [Export]
        Enum_DamageType enum_DamageType;

        [Export]
        Range_Res_Base range_Res_Base;
        public Range_Res_Base Skill_Range_Res_Base => range_Res_Base;

        protected override void CallSpelledSkill() {
            float physicalDamage = callSkillObject.PhysicalDamageAmount(damage);
            float magicDamage = callSkillObject.MagicDamageAmount(damage);

            foreach (UnitState testObject in testObjects) {
                if (testObject.Camp == callSkillObject.Camp) {
                    continue;
                }

                bool isInRange = range_Res_Base.IsInRange(callSkillObject, testObject, targetPos);

                if (isInRange) {
                    if (enum_DamageType == Enum_DamageType.Physcial) {
                        testObject.TakeDamage(physicalDamage, Enum_DamageType.Physcial);
                    }
                    else if (enum_DamageType == Enum_DamageType.Magic) {
                        testObject.TakeDamage(magicDamage, Enum_DamageType.Magic);
                    }
                }

            }
        }

    }
}
