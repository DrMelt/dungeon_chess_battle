using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;

namespace DungeonChessBattle.Battle.Logic.Combat;

/// <summary>
/// 技能施放静态判定唯一来源。三处判据共用同一实现：服务端权威校验、施法预输入缓冲的重试、
/// 战斗世界应答内容侧的 <see cref="DungeonChessBattle.Battle.Shared.IBattleSceneView.CanCast"/> 问答。
/// 基于施法单位状态、技能定义与已解析的目标/位置判断，不接触技能仓库。
/// 在线端不做任何本地施法判定：按键即上行，可否施放由本判定在权威侧裁定。
/// </summary>
public static class SkillCastValidator {
    /// <summary>
    /// 判定单位能否发起指定技能的施法：归属、施法者状态与目标/位置因素聚合。
    /// 不判目标存活：<see cref="SkillTargetValidator.CanAffect"/> 只比阵营关系，死亡单位仍是合法目标，
    /// 结算侧亦不拦，故对死亡单位施放治疗或 HoT 会经 ApplyHealthDelta 把它抬回存活。
    /// 泛型约束收敛为施法判定子集 <see cref="ISkillCasterView"/>，服务端与回放的 <see cref="BattleUnit"/> 及 AI 视图均可传入。
    /// </summary>
    /// <param name="caster">施法单位只读视图。</param>
    /// <param name="skill">目标技能定义。</param>
    /// <param name="target">已解析的单位目标；无单位目标需求时传 null。</param>
    /// <param name="targetPos">已解析的位置目标；无位置目标需求时传 null。</param>
    /// <param name="relations">副本配置的阵营关系函数。</param>
    public static bool CanCast<T>(T caster, SkillDefinition skill, T? target, Vector2? targetPos,
        CampRelationResolver relations)
        where T : ISkillCasterView {
        if (!caster.HasSkill(skill.SkillId))
            return false;
        if (!IsStateReady(caster, skill.SkillId))
            return false;
        if (skill.NeedUnitTarget)
            return target is not null
                && SkillTargetValidator.CanAffect(caster, target, skill.TargetPolicy, relations)
                && IsUnitTargetInRange(caster, target, skill);
        if (skill.NeedPosTarget)
            return IsTargetPosInRange(caster, skill, targetPos);
        return true;
    }

    /// <summary>单位目标距离因素：CastRange 大于 0 时要求中心距含双方碰撞半径不超过射程，0 视为不设限。</summary>
    private static bool IsUnitTargetInRange<T>(T caster, T target, SkillDefinition skill)
        where T : ISkillCasterView {
        if (skill.CastRange <= 0f)
            return true;
        float reach = skill.CastRange + caster.BodyRadius + target.BodyRadius;
        return Vector2.Distance(caster.Position, target.Position) <= reach;
    }

    /// <summary>
    /// 状态就绪判据：存活、非读条与技能总冷却（全局与个体取较大）均就绪。单值查询，无托管对象分配。
    /// 除 <see cref="CanCast"/> 内部聚合外，亦是施法预输入缓冲的唯一重试判据：
    /// 只有会自然转就绪的状态阻塞值得等待，目标条件一律交落地时裁定。
    /// </summary>
    public static bool IsStateReady<T>(T caster, SkillKeyId skillKey)
        where T : ISkillCasterView {
        if (caster.IsDead || caster.SkillCasting != default)
            return false;
        return caster.GetTotalCooldownRemaining(skillKey) <= 0f;
    }

    /// <summary>位置因素：目标点非空且落在技能有效范围内，读取定义形状判定。</summary>
    private static bool IsTargetPosInRange<T>(T caster, SkillDefinition skill, Vector2? targetPos)
        where T : ISkillCasterView {
        return targetPos is { } pos && skill.CastArea is { } area
            && area.Contains(pos, caster.Position, pos - caster.Position, 0f);
    }
}
