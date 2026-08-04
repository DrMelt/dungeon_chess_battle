using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Battle;

public class BattleResolver {
    public static void ApplySkillDamage(IUnitState caster, IUnitState target, SkillDamageModel skill) {
        float rawDamage = skill.DamageType == Enum_DamageType.Physcial
            ? caster.PhysicalDamageAmount(skill.Damage)
            : caster.MagicDamageAmount(skill.Damage);
        target.TakeDamage(rawDamage, skill.DamageType);
    }

    public static void ApplySkillCure(IUnitState caster, IUnitState target, SkillCureModel skill) {
        float rawCure = caster.CureAmount(skill.CurePotency);
        target.RestoreHealth(rawCure);
    }

    public static void ApplySkillRangeDamage(IUnitState caster, IReadOnlyList<IUnitState> allUnits,
        SkillRangeDamageModel skill) {
        float physicalDamage = caster.PhysicalDamageAmount(skill.Damage);
        float magicDamage = caster.MagicDamageAmount(skill.Damage);

        foreach (var testUnit in allUnits) {
            if (testUnit.Camps.Any(c => caster.Camps.Contains(c)))
                continue;

            if (skill.RangeRes == null)
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

    public static void ApplySkillAddBuff(IUnitState target, SkillAddBuffModel skill) {
        if (skill.Buff == null)
            throw new System.InvalidOperationException("[BattleResolver] SkillAddBuffModel.Buff is null.");
        target.AddBuff(skill.Buff);
    }

    public static void UpdateUnitBuffs(IUnitState unit, double deltaTime) {
        unit.UpdateBuffList(deltaTime);
    }

    public static bool HasAliveUnits(IEnumerable<IUnitState> units) {
        return units.Any(u => u.Health > 0);
    }
}
