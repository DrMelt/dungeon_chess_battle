using System.Numerics;
using DungeonChessBattle.Battle.Shared;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Intelligence;
using DungeonChessBattle.Battle.Logic.Combat;

namespace DungeonChessBattle.Battle.Logic.Intelligence;

/// <summary>
/// 默认敌人决策模块：为单个敌方单位生成当帧动作意图。
/// 纯函数式决策，依赖 <see cref="IBattleUnitView"/> 只读契约；阵营关系由调用方按副本运行时注入，不绑定实例；
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
            return EnemyDecision.Cast(skill.SkillId, target.UnitId, anchor);
        }

        return EnemyDecision.Idle();
    }

    /// <summary>选目标：存活敌对单位中仇恨最高者优先，全零仇恨回退距自身最近者。</summary>
    private static IBattleUnitView? SelectTarget(IBattleUnitView self, IReadOnlyList<IBattleUnitView> candidates,
        CampRelationResolver relations) {
        var selfPos = self.Snapshot.Position;
        IBattleUnitView? topTarget = null;
        float topHate = 0f;
        IBattleUnitView? nearest = null;
        float nearestDistanceSq = float.MaxValue;

        foreach (var candidate in candidates) {
            if (candidate == self || candidate.IsDead)
                continue;
            if (relations.Invoke(self.Camps, candidate.Camps) != CampRelation.Enemy)
                continue;

            float hate = self.HateOf(candidate.UnitId);
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

    /// <summary>
    /// 停靠距离：敌方目标技能中最远射程，属 AI 逼近偏好而非施法权威判定。
    /// 单位目标技能取 CastRange，位置目标技能取形状 FarReach，由技能数据配置。
    /// </summary>
    private float ApproachRange(IBattleUnitView self) {
        float range = 0f;
        foreach (var skill in self.Skills) {
            if (!skill.TargetPolicy.HasFlag(SkillTargetPolicy.Different))
                continue;

            float reach = skill.CastArea?.FarReach ?? skill.CastRange;
            if (reach > range)
                range = reach;
        }
        return range > 0f ? range : _fallbackApproachRange;
    }
}
