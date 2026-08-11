using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Range;

namespace DungeonChessBattle.Battle.Logic.Combat;

/// <summary>
/// 技能效果结算的无状态纯函数。只做数值计算与范围判定，不持有状态、不产生副作用；
/// 状态写回（Health 等）由编排层（BattleRoom）依据返回值完成。
/// </summary>
public static class CastResolver {
    /// <summary>结算一次单体/范围伤害：按施法者攻击系数与受击者承受系数折算。</summary>
    public static DamageResult ComputeDamage(UnitSnapshot caster, UnitSnapshot target, float baseDamage, DamageType type)
        => DamageProcessor.Process(caster, target, baseDamage, type);

    /// <summary>结算一次治疗：按施法者治疗强度折算并钳制生命上限。</summary>
    public static HealResult ComputeHeal(UnitSnapshot healer, UnitSnapshot target, float potency)
        => HealProcessor.Process(healer, target, potency);

    /// <summary>
    /// 判断目标是否处于以施法者为锚点、沿攻击方向的范围内。
    /// </summary>
    /// <param name="range">范围形状。</param>
    /// <param name="caster">施法者快照。</param>
    /// <param name="target">被检测目标快照。</param>
    /// <param name="aimDirection">技能朝向向量（未归一化亦可，内部归一化）。</param>
    public static bool IsInRange(RangeShape range, UnitSnapshot caster, UnitSnapshot target, Vector2 aimDirection)
        => range.Contains(target.Position, caster.Position, aimDirection, target.BodyRadius);
}
