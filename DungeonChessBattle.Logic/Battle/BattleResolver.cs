using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Battle;

public class BattleResolver
{
    public void ApplySkillDamage(UnitModel caster, UnitModel target, SkillModel skill)
    {
        if (skill is SkillDamageModel damageSkill)
        {
            float rawDamage = caster.PhysicalDamageAmount(damageSkill.Damage);
            target.TakeDamage(rawDamage, damageSkill.DamageType);
        }
    }

    public void ApplySkillCure(UnitModel caster, UnitModel target, SkillModel skill)
    {
        if (skill is SkillCureModel cureSkill)
        {
            float rawCure = caster.CureAmount(cureSkill.CurePotency);
            target.RestoreHealth(rawCure);
        }
    }

    public void UpdateUnitBuffs(UnitModel unit, double deltaTime)
    {
        unit.UpdateBuffList(deltaTime);
    }

    public bool HasAliveUnits(IReadOnlyList<UnitModel> units)
    {
        return units.Any(u => u.Health > 0);
    }
}