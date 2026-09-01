using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;

namespace DungeonChessBattle.Battle.Logic.Combat;

/// <summary>
/// 技能目标阵营关系校验。服务端权威校验与在线端目标选择共用同一判定逻辑。
/// 敌我关系由副本配置的 CampRelationResolver 决定，调用方必须就绪后注入。
/// Unknown 视为不可判定，受阵营策略限定的技能一律拒绝；空阵营不可判定，按不可作用处理。
/// </summary>
public static class SkillTargetValidator {
    /// <summary>
    /// 判定按目标阵营关系限定的技能能否作用于目标。
    /// Same 仅限友方，Different 仅限敌方，None 不可主动选择单位目标。
    /// </summary>
    /// <param name="source">施法单位只读视图。</param>
    /// <param name="target">目标单位只读视图。</param>
    /// <param name="policy">技能目标阵营策略。</param>
    /// <param name="resolver">副本配置的阵营关系函数。</param>
    public static bool CanAffect(IUnitIdentityView source, IUnitIdentityView target, SkillTargetPolicy policy,
        CampRelationResolver resolver) {
        if (policy == SkillTargetPolicy.None)
            return false;
        if (source.Camps.Count == 0 || target.Camps.Count == 0)
            return false;
        var relation = resolver.Invoke(source.Camps, target.Camps);
        return (relation == CampRelation.Friendly && policy.HasFlag(SkillTargetPolicy.Same))
            || (relation == CampRelation.Enemy && policy.HasFlag(SkillTargetPolicy.Different));
    }
}
