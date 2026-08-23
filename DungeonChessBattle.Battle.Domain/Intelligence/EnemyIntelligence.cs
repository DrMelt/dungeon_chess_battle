using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Enums;

namespace DungeonChessBattle.Battle.Domain.Intelligence;

/// <summary>敌人 AI 决策结果类型：当前帧对单个敌方的动作意图。</summary>
public enum EnemyDecisionKind {
    /// <summary>无动作：停留原地。</summary>
    Idle,
    /// <summary>逼近目标，朝给定方向移动。</summary>
    MoveTo,
    /// <summary>对目标施放指定技能。</summary>
    CastSkill,
}

/// <summary>
/// 敌人 AI 决策结果，纯数据不持引用。
/// 单位目标经 TargetNetId 表达，位置锚点经 TargetPosition 表达，方向经 MoveDirection 表达。
/// </summary>
public readonly record struct EnemyDecision(
    EnemyDecisionKind Kind,
    ushort TargetNetId = 0,
    SkillKeyId SkillId = default,
    Vector2 TargetPosition = default,
    Vector2 MoveDirection = default) {
    /// <summary>原地等待决策。</summary>
    public static EnemyDecision Idle() => new(EnemyDecisionKind.Idle);

    /// <summary>朝目标方向逼近决策。</summary>
    public static EnemyDecision MoveTo(Vector2 moveDirection) => new(EnemyDecisionKind.MoveTo, MoveDirection: moveDirection);

    /// <summary>对指定目标施放技能决策，targetPosition 为施法锚点。</summary>
    public static EnemyDecision Cast(SkillKeyId skillId, ushort targetNetId, Vector2 targetPosition)
        => new(EnemyDecisionKind.CastSkill, targetNetId, skillId, targetPosition);
}

/// <summary>敌人智能默认参数常量，实现与配置构造共用。</summary>
public static class EnemyIntelligenceDefaults {
    /// <summary>技能未配置射程时的兜底逼近距离。</summary>
    public const float ApproachRange = 10f;
}

/// <summary>
/// 敌人单位决策契约。实现必须无状态，无状态实例可被任意多个单位共享。
/// 决策只依赖 IBattleUnit 只读契约与调用方按副本注入的阵营关系，不接触网络载体，可脱离服务端独立测试。
/// </summary>
public interface IUnitIntelligence {
    /// <summary>
    /// 生成敌方单位当帧决策：选目标，按目标距离决定逼近或施法。
    /// 仅房间线程调用，输入在本帧内不应变化。
    /// </summary>
    /// <param name="self">决策主体，仇恨取自其自身仇恨投影。</param>
    /// <param name="scene">战场查询视图，本帧读只读，禁止写。</param>
    /// <param name="relations">所在副本的阵营关系函数，敌我判定唯一来源。</param>
    EnemyDecision Decide(IBattleUnitView self, IBattleSceneView scene, CampRelationResolver relations);
}

/// <summary>
/// 默认敌人决策模块：为单个敌方单位生成当帧动作意图。
/// 纯函数式决策，依赖 IBattleUnit 只读契约；阵营关系由调用方按副本运行时注入，不绑定实例；
/// 施法判定复用 <see cref="SkillCastValidator"/> 唯一来源，目标选择以仇恨为优先，无仇恨回退最近者；射程一律取自技能配置而非魔数。
/// </summary>
/// <param name="fallbackApproachRange">技能未配置射程时的兜底逼近距离，默认 10 与迁改前 AttackRange 常量一致。</param>
public sealed class EnemyIntelligence(
    float fallbackApproachRange = EnemyIntelligenceDefaults.ApproachRange) : IUnitIntelligence {
    private readonly float _fallbackApproachRange = fallbackApproachRange;

    /// <inheritdoc />
    public EnemyDecision Decide(IBattleUnitView self, IBattleSceneView scene, CampRelationResolver relations) {
        // 正在读条：原地等待读条完成，避免移动打断自身读条
        if (self.SkillCasting != default)
            return EnemyDecision.Idle();

        var target = SelectTarget(self, scene.Units, relations);
        if (target == null)
            return EnemyDecision.Idle();

        float distance = Vector2.Distance(self.Snapshot.Position, target.Snapshot.Position);
        if (distance > ApproachRange(self))
            return EnemyDecision.MoveTo(target.Snapshot.Position - self.Snapshot.Position);

        // 已进入停靠距离：按技能配置顺序找首个可命中技能，锚点恒为已选目标当前位置
        foreach (var skill in self.Skills) {
            Vector2 anchor = target.Snapshot.Position;
            if (!SkillCastValidator.CanCast(self, skill, target, anchor, relations))
                continue;
            return EnemyDecision.Cast(skill.SkillId, target.UnitNetId, anchor);
        }

        return EnemyDecision.Idle();
    }

    /// <summary>选目标：存活敌对单位中仇恨最高者优先，全零仇恨回退距自身最近者。</summary>
    private static IBattleUnitView? SelectTarget(IBattleUnitView self, IReadOnlyList<IBattleUnitView> candidates,
        CampRelationResolver relations) {
        var selfPos = self.Snapshot.Position;
        var hates = BuildHateLookup(self);
        IBattleUnitView? topTarget = null;
        float topHate = 0f;
        IBattleUnitView? nearest = null;
        float nearestDistanceSq = float.MaxValue;

        foreach (var candidate in candidates) {
            if (candidate == self || candidate.Health <= 0f)
                continue;
            if (relations.Invoke(self.Camps, candidate.Camps) != CampRelation.Enemy)
                continue;

            float hate = hates.GetValueOrDefault(candidate.UnitNetId);
            if (hate > topHate) {
                topHate = hate;
                topTarget = candidate;
            }

            float distanceSq = Vector2.DistanceSquared(selfPos, candidate.Snapshot.Position);
            if (distanceSq < nearestDistanceSq) {
                nearestDistanceSq = distanceSq;
                nearest = candidate;
            }
        }

        return topTarget ?? nearest;
    }

    /// <summary>把单位自身仇恨投影整理为按目标网络 ID 查询的字典，目标选择读取用。</summary>
    private static Dictionary<ushort, float> BuildHateLookup(IBattleUnitView self) {
        var hates = new Dictionary<ushort, float>(self.Hates.Count);
        foreach (var snapshot in self.Hates)
            hates[snapshot.TargetNetId] = snapshot.Value;
        return hates;
    }

    /// <summary>
    /// 停靠距离：敌方目标技能中最远射程，属 AI 逼近偏好而非施法权威判定。
    /// 单位目标技能取 CastRange，位置目标技能取 RangeShape.FarReach。
    /// </summary>
    private float ApproachRange(IBattleUnitView self) {
        float range = 0f;
        foreach (var skill in self.Skills) {
            if (!skill.TargetPolicy.HasFlag(SkillTargetPolicy.Different))
                continue;

            float reach = skill switch {
                RangeDamageSkillDefinition rangeSkill => rangeSkill.Range.FarReach,
                _ => skill.CastRange > 0f ? skill.CastRange : _fallbackApproachRange,
            };
            if (reach > range)
                range = reach;
        }
        return range > 0f ? range : _fallbackApproachRange;
    }

}
