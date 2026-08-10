using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Battle;

/// <summary>
/// 技能效果结算器：根据技能模型类型对目标（或范围内全体）执行伤害、治疗、Buff 施加等结算。
/// </summary>
public class BattleResolver {
    /// <summary>
    /// 结算单体伤害：按伤害类型以施法者对应攻击系数换算伤害后作用于目标。
    /// </summary>
    /// <param name="caster">施法单位。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">单体伤害技能模型。</param>
    public static void ApplySkillDamage(IUnitState caster, IUnitState target, SkillDamageModel skill) {
        float rawDamage = skill.DamageType == DamageType.Physical
            ? caster.PhysicalDamageAmount(skill.Damage)
            : caster.MagicDamageAmount(skill.Damage);
        target.TakeDamage(rawDamage, skill.DamageType);
    }

    /// <summary>
    /// 结算治疗：以施法者治疗强度换算后恢复目标生命值。
    /// </summary>
    /// <param name="caster">施法单位。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">治疗技能模型。</param>
    public static void ApplySkillCure(IUnitState caster, IUnitState target, SkillCureModel skill) {
        float rawCure = caster.CureAmount(skill.CurePotency);
        target.RestoreHealth(rawCure);
    }

    /// <summary>
    /// 结算范围伤害：筛选与施法者不同阵营且处于技能范围内的目标并造成伤害。
    /// </summary>
    /// <param name="caster">施法单位。</param>
    /// <param name="allUnits">所有可被命中的检测单位。</param>
    /// <param name="skill">范围伤害技能模型。</param>
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

            if (skill.DamageType == DamageType.Physical)
                testUnit.TakeDamage(physicalDamage, DamageType.Physical);
            else if (skill.DamageType == DamageType.Magic)
                testUnit.TakeDamage(magicDamage, DamageType.Magic);
        }
    }

    /// <summary>
    /// 结算施加 Buff：将技能携带的 Buff 添加到目标单位。
    /// </summary>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">施加 Buff 的技能模型。</param>
    /// <exception cref="InvalidOperationException">skill.Buff 为 null 时抛出。</exception>
    public static void ApplySkillAddBuff(IUnitState target, SkillAddBuffModel skill) {
        if (skill.Buff == null)
            throw new InvalidOperationException("[BattleResolver] SkillAddBuffModel.Buff is null.");
        target.AddBuff(skill.Buff);
    }

    /// <summary>
    /// 按帧推进单位的 Buff 状态。
    /// </summary>
    /// <param name="unit">目标单位。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    public static void UpdateUnitBuffs(IUnitState unit, double deltaTime) {
        unit.UpdateBuffList(deltaTime);
    }

    /// <summary>
    /// 判断集合中是否存在存活单位（生命值大于 0）。
    /// </summary>
    /// <param name="units">单位集合。</param>
    /// <returns>存在存活单位返回 true。</returns>
    public static bool HasAliveUnits(IEnumerable<IUnitState> units) {
        return units.Any(u => u.Health > 0);
    }
}
