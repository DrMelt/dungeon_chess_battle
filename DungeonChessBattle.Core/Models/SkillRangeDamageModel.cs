using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class SkillRangeDamageModel : SkillModel {
    public float Damage {
        get; set;
    }
    public Enum_DamageType DamageType {
        get; set;
    }
    public IRangeRes RangeRes { get; set; } = null!;

    protected override void CallSpelledSkill() {
        float physicalDamage = CallSkillObject.PhysicalDamageAmount(Damage);
        float magicDamage = CallSkillObject.MagicDamageAmount(Damage);

        foreach (IUnitState testObject in TestObjects) {
            if (testObject.Camp == CallSkillObject.Camp)
                continue;

            bool isInRange = RangeRes.IsInRange(CallSkillObject, testObject, TargetPos);

            if (isInRange) {
                if (DamageType == Enum_DamageType.Physcial) {
                    testObject.TakeDamage(physicalDamage, Enum_DamageType.Physcial);
                }
                else if (DamageType == Enum_DamageType.Magic) {
                    testObject.TakeDamage(magicDamage, Enum_DamageType.Magic);
                }
            }
        }
    }
}
