using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Enums;

namespace DungeonChessBattle.Battle.Logic.Ai;

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

/// <summary>
/// 敌人 AI 决策模块：为单个敌方单位生成当帧动作意图。
/// 纯函数式决策，只依赖 IBattleUnit 只读契约与阵营关系，
/// 目标选择直读单位自身仇恨投影（IBattleUnit.Hates），不接触网络载体，可脱离服务端独立测试。
/// 目标选择以仇恨为优先，无仇恨回退最近者；射程一律取自技能配置而非魔数。
/// </summary>
/// <param name="relations">副本配置的阵营关系函数，敌我判定唯一来源。</param>
/// <param name="fallbackApproachRange">技能未配置射程时的兜底逼近距离，默认 10 与迁改前 AttackRange 常量一致。</param>
public sealed class EnemyIntelligence(
    CampRelationResolver relations,
    float fallbackApproachRange = 10f) {
    private readonly CampRelationResolver _relations = relations;
    private readonly float _fallbackApproachRange = fallbackApproachRange;

    /// <summary>
    /// 生成敌方单位当帧决策：选目标，按目标距离决定逼近或施法。
    /// 仅房间线程调用，输入在本帧内不应变化。
    /// </summary>
    /// <param name="self">决策主体，仇恨取自其自身仇恨投影。</param>
    /// <param name="candidates">本房间全部战斗单位，内部自行筛选存活敌对目标。</param>
    public EnemyDecision Decide(IBattleUnit self, IReadOnlyList<IBattleUnit> candidates) {
        var target = SelectTarget(self, candidates);
        if (target == null)
            return EnemyDecision.Idle();

        float distance = Vector2.Distance(self.Snapshot.Position, target.Snapshot.Position);
        if (distance > ApproachRange(self))
            return EnemyDecision.MoveTo(target.Snapshot.Position - self.Snapshot.Position);

        // 已进入停靠距离：按技能配置顺序找首个就绪且可命中的技能
        foreach (var skill in self.Skills) {
            if (!CanCastNow(self, skill))
                continue;
            Vector2? anchor = ResolveCastAnchor(self, skill, target, distance);
            if (anchor is { } targetPos)
                return EnemyDecision.Cast(skill.SkillId, target.UnitNetId, targetPos);
        }

        return EnemyDecision.Idle();
    }

    /// <summary>选目标：存活敌对单位中仇恨最高者优先，全零仇恨回退距自身最近者。</summary>
    private IBattleUnit? SelectTarget(IBattleUnit self, IReadOnlyList<IBattleUnit> candidates) {
        var selfPos = self.Snapshot.Position;
        var hates = BuildHateLookup(self);
        IBattleUnit? topTarget = null;
        float topHate = 0f;
        IBattleUnit? nearest = null;
        float nearestDistanceSq = float.MaxValue;

        foreach (var candidate in candidates) {
            if (candidate == self || candidate.Health <= 0f)
                continue;
            if (_relations.Invoke(self.Camps, candidate.Camps) != CampRelation.Enemy)
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
    private static Dictionary<ushort, float> BuildHateLookup(IBattleUnit self) {
        var hates = new Dictionary<ushort, float>(self.Hates.Count);
        foreach (var snapshot in self.Hates)
            hates[snapshot.TargetNetId] = snapshot.Value;
        return hates;
    }

    /// <summary>停靠距离：敌方目标技能中最远射程。单位目标技能取 CastRange，位置目标技能取 RangeShape.FarReach。</summary>
    private float ApproachRange(IBattleUnit self) {
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

    /// <summary>施法状态因素：存活、非读条、全局冷却与个体冷却都就绪。</summary>
    private static bool CanCastNow(IBattleUnit self, SkillDefinition skill)
        => self.Health > 0f
            && self.SkillCasting == default
            && self.GcdRemaining <= 0f
            && self.GetSkillCooldownRemaining(skill.SkillId) <= 0f;

    /// <summary>解析技能在当前距离与目标关系下能否命中，命中则返回施法锚点，否则 null。</summary>
    private Vector2? ResolveCastAnchor(IBattleUnit self, SkillDefinition skill, IBattleUnit target, float distance) {
        var selfPos = self.Snapshot.Position;
        var targetPos = target.Snapshot.Position;

        if (skill.NeedUnitTarget) {
            if (!SkillTargetValidator.CanAffect(self, target, skill.TargetPolicy, _relations))
                return null;
            if (skill.CastRange <= 0f)
                return targetPos;
            float reach = skill.CastRange + self.Snapshot.BodyRadius + target.Snapshot.BodyRadius;
            return distance <= reach ? targetPos : null;
        }

        if (skill.NeedPosTarget) {
            if (skill is not RangeDamageSkillDefinition rangeSkill)
                return null;
            return rangeSkill.Range.Contains(targetPos, selfPos, targetPos - selfPos, 0f) ? targetPos : null;
        }

        return targetPos;
    }
}
