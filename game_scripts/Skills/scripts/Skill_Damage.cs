using Godot;


namespace GameLogic {
    [GlobalClass]
    public partial class Skill_Damage : UnitSkillBase {


        [Export]
        float damage = 0;
        [Export]
        Enum_DamageType enum_DamageType;

        protected override void CallSpelledSkill() {
            if (enum_DamageType == Enum_DamageType.Physcial) {
                float damageAmount = callSkillObject.PhysicalDamageAmount(damage);
                targetObject.TakeDamage(damageAmount, Enum_DamageType.Physcial);
            }
            else if (enum_DamageType == Enum_DamageType.Magic) {
                float damageAmount = callSkillObject.MagicDamageAmount(damage);
                targetObject.TakeDamage(damageAmount, Enum_DamageType.Magic);
            }
        }
    }
}
