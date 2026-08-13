namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 技能目标阵营关系校验。服务端权威校验与客户端预拦共用同一判定逻辑。
/// </summary>
public static class SkillTargetValidator {
    /// <summary>
    /// 判断两个单位是否属于同一阵营，存在任一共同阵营即视为友方。
    /// </summary>
    public static bool IsSameCamp(IBattleUnit source, IBattleUnit target)
        => source.Camps.Any(c => target.Camps.Contains(c));

    /// <summary>
    /// 判定按目标阵营关系限定的技能能否作用于目标。
    /// Same 仅限友方，Different 仅限敌方，None 不可主动选择单位目标。
    /// </summary>
    public static bool CanAffect(IBattleUnit source, IBattleUnit target, SkillTargetPolicy policy) {
        if (policy == SkillTargetPolicy.None)
            return false;
        bool sameCamp = IsSameCamp(source, target);
        return (sameCamp && policy.HasFlag(SkillTargetPolicy.Same))
            || (!sameCamp && policy.HasFlag(SkillTargetPolicy.Different));
    }
}
