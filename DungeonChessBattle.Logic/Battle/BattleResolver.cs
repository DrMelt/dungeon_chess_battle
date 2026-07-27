using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Battle;

public class BattleResolver {
    public static void ApplySkillDamage(UnitModel caster, UnitModel target, SkillDamageModel skill) {
        float rawDamage = skill.DamageType == Enum_DamageType.Physcial
            ? caster.PhysicalDamageAmount(skill.Damage)
            : caster.MagicDamageAmount(skill.Damage);
        target.TakeDamage(rawDamage, skill.DamageType);
    }

    public static void ApplySkillCure(UnitModel caster, UnitModel target, SkillCureModel skill) {
        float rawCure = caster.CureAmount(skill.CurePotency);
        target.RestoreHealth(rawCure);
    }

    public static void ApplySkillRangeDamage(UnitModel caster, IReadOnlyList<UnitModel> allUnits,
        SkillRangeDamageModel skill) {
        float physicalDamage = caster.PhysicalDamageAmount(skill.Damage);
        float magicDamage = caster.MagicDamageAmount(skill.Damage);

        foreach (var testUnit in allUnits) {
            if (testUnit.Camp == caster.Camp)
                continue;

            bool isInRange = skill.RangeRes.IsInRange(caster, testUnit, skill.TargetPos);
            if (!isInRange)
                continue;

            if (skill.DamageType == Enum_DamageType.Physcial)
                testUnit.TakeDamage(physicalDamage, Enum_DamageType.Physcial);
            else if (skill.DamageType == Enum_DamageType.Magic)
                testUnit.TakeDamage(magicDamage, Enum_DamageType.Magic);
        }
    }

    public static void ApplySkillAddBuff(UnitModel target, SkillAddBuffModel skill) {
        target.AddBuff(skill.Buff);
    }

    public static void UpdateUnitBuffs(UnitModel unit, double deltaTime) {
        unit.UpdateBuffList(deltaTime);
    }

    public static bool HasAliveUnits(IReadOnlyList<UnitModel> units) {
        return units.Any(u => u.Health > 0);
    }
}
