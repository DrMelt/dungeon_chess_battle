using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;

namespace DungeonChessBattle.Battle.Shared.Intelligence;

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
/// 决策只依赖 <see cref="IBattleUnitView"/> 只读契约与调用方按副本注入的阵营关系，不接触网络载体，可脱离服务端独立测试。
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
