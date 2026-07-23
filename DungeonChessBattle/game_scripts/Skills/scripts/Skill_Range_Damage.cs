using DungeonChessBattle.Core.Interfaces;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Range_Damage : UnitSkillBaseGodot {

    [Export]
    float damage = 0;

    [Export]
    Enum_DamageType enum_DamageType;

    [Export]
    RangeResBaseGodot range_Res_Base;
    public RangeResBaseGodot Skill_Range_Res_Base => range_Res_Base;

    protected override void CallSpelledSkill() {
        float physicalDamage = CallSkillObject.PhysicalDamageAmount(damage);
        float magicDamage = CallSkillObject.MagicDamageAmount(damage);

        foreach (IUnitState testObject in TestObjects) {
            if (testObject.Camp == CallSkillObject.Camp) {
                continue;
            }

            bool isInRange = range_Res_Base.IsInRange(CallSkillObject, testObject, TargetPos);

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
