using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Enums;

namespace DungeonChessBattle.Battle.Logic.Combat;

/// <summary>
/// 技能施放静态判定，服务端权威校验与客户端预输入共用同一实现。
/// 基于施法单位状态、技能定义与已解析的目标/位置判断，不接触技能仓库。
/// </summary>
public static class SkillCastValidator {
    /// <summary>
    /// 判定单位能否发起指定技能的施法：归属、状态与目标/位置因素全部聚合。
    /// </summary>
    /// <param name="caster">施法单位。</param>
    /// <param name="skill">目标技能定义。</param>
    /// <param name="target">已解析的单位目标；无单位目标需求时传 null。</param>
    /// <param name="targetPos">已解析的位置目标；无位置目标需求时传 null。</param>
    /// <param name="relations">副本配置的阵营关系函数。</param>
    public static bool CanCast(IBattleUnit caster, SkillDefinition skill, IBattleUnit? target, Vector2? targetPos,
        CampRelationResolver relations) {
        if (!caster.HasSkill(skill.SkillId))
            return false;
        if (!CanCastState(caster, skill.SkillId))
            return false;
        if (skill.NeedUnitTarget)
            return target != null
                && SkillTargetValidator.CanAffect(caster, target, skill.TargetPolicy, relations)
                && IsUnitTargetInRange(caster, target, skill);
        if (skill.NeedPosTarget)
            return IsTargetPosInRange(caster, skill, targetPos);
        return true;
    }

    /// <summary>单位目标距离因素：CastRange 大于 0 时要求中心距含双方碰撞半径不超过射程，0 视为不设限。</summary>
    private static bool IsUnitTargetInRange(IBattleUnit caster, IBattleUnit target, SkillDefinition skill) {
        if (skill.CastRange <= 0f)
            return true;
        float reach = skill.CastRange + caster.Snapshot.BodyRadius + target.Snapshot.BodyRadius;
        return Vector2.Distance(caster.Snapshot.Position, target.Snapshot.Position) <= reach;
    }

    /// <summary>状态因素聚合：存活、非读条与全局/个体冷却均就绪。单值查询，无托管对象分配。</summary>
    private static bool CanCastState(IBattleUnit caster, SkillKeyId skillKey) {
        if (caster.Health <= 0f || caster.SkillCasting != default || caster.GcdRemaining > 0f)
            return false;
        return caster.GetSkillCooldownRemaining(skillKey) <= 0f;
    }

    /// <summary>位置因素：目标点非空且落在技能几何范围内，与结算共用 RangeShape 判定。</summary>
    private static bool IsTargetPosInRange(IBattleUnit caster, SkillDefinition skill, Vector2? targetPos) {
        if (targetPos is not { } pos || skill is not RangeDamageSkillDefinition rangeSkill)
            return false;
        var origin = caster.Snapshot.Position;
        return rangeSkill.Range.Contains(pos, origin, pos - origin, 0f);
    }
}
