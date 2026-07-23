using Godot;


namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Damage : UnitSkillBaseGodot {


    [Export]
    float damage = 0;
    [Export]
    Enum_DamageType enum_DamageType;

    protected override void CallSpelledSkill() {
        if (enum_DamageType == Enum_DamageType.Physcial) {
            float damageAmount = CallSkillObject.PhysicalDamageAmount(damage);
            TargetObject.TakeDamage(damageAmount, Enum_DamageType.Physcial);
        }
        else if (enum_DamageType == Enum_DamageType.Magic) {
            float damageAmount = CallSkillObject.MagicDamageAmount(damage);
            TargetObject.TakeDamage(damageAmount, Enum_DamageType.Magic);
        }
    }
}
