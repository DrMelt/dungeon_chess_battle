using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Battle;

public class BattleResolver {
    public static void ApplySkillDamage(UnitModel caster, UnitModel target, SkillModel skill) {
        if (skill is SkillDamageModel damageSkill) {
            float rawDamage = caster.PhysicalDamageAmount(damageSkill.Damage);
            target.TakeDamage(rawDamage, damageSkill.DamageType);
        }
    }

    public static void ApplySkillCure(UnitModel caster, UnitModel target, SkillModel skill) {
        if (skill is SkillCureModel cureSkill) {
            float rawCure = caster.CureAmount(cureSkill.CurePotency);
            target.RestoreHealth(rawCure);
        }
    }

    public static void UpdateUnitBuffs(UnitModel unit, double deltaTime) {
        unit.UpdateBuffList(deltaTime);
    }

    public static bool HasAliveUnits(IReadOnlyList<UnitModel> units) {
        return units.Any(u => u.Health > 0);
    }
}
