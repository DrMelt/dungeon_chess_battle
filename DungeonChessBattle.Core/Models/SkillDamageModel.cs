using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Models;

public class SkillDamageModel : SkillModel {
    public float Damage {
        get; set;
    }
    public Enum_DamageType DamageType {
        get; set;
    }

    protected override void CallSpelledSkill() {
        if (DamageType == Enum_DamageType.Physcial) {
            float damageAmount = CallSkillObject.PhysicalDamageAmount(Damage);
            TargetObject.TakeDamage(damageAmount, Enum_DamageType.Physcial);
        }
        else if (DamageType == Enum_DamageType.Magic) {
            float damageAmount = CallSkillObject.MagicDamageAmount(Damage);
            TargetObject.TakeDamage(damageAmount, Enum_DamageType.Magic);
        }
    }
}
