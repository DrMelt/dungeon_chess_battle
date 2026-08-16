using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Intelligence;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Entities;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 敌人大脑编排壳：服务端每 tick 为存活敌人做决策并映射为移动输入与施法请求。
/// 决策由各单位注入的 <see cref="IUnitIntelligence"/> 提供，本类只做存活调度与决策到载体动作的映射；
/// 移动与施法执行经 <see cref="BattleEngine"/> 与玩家侧共用同一权威入口。
/// </summary>
/// <param name="battleEngine">本房间的战斗编排实例，施法与移动打断读条入口。</param>
/// <param name="relations">所在副本的阵营关系函数，注入给全部敌人决策。</param>
/// <param name="logger">日志器。</param>
public sealed class EnemyBrain(
    BattleEngine battleEngine,
    CampRelationResolver relations,
    ILogger<EnemyBrain> logger) {
    private readonly ILogger<EnemyBrain> _logger = logger;

    /// <summary>战斗编排引用，驱动移动与读条入口。</summary>
    private readonly BattleEngine _battleEngine = battleEngine;

    /// <summary>所在副本的阵营关系函数，作为决策敌我判定的运行时输入。</summary>
    private readonly CampRelationResolver _relations = relations;

    /// <summary>
    /// 服务端每逻辑 tick 调用，驱动全部存活敌人。仅房间线程调用。
    /// 必须在 EntityManager.Update() 之前执行，确保移动输入先于单位位移结算写入；仅在 Running 阶段驱动。
    /// </summary>
    /// <param name="enemies">本房间的敌人 Pawn 列表。</param>
    /// <param name="allPawns">本房间全部 Pawn，用作决策候选池。</param>
    public void Tick(IReadOnlyList<UnitPawn> enemies, IReadOnlyList<UnitPawn> allPawns) {
        if (_battleEngine.CurrentPhase != BattlePhase.Running)
            return;
        if (enemies.Count == 0 || allPawns.Count == 0)
            return;

        // 决策场景每帧组装一次，候选池与目标索引供全部敌人共享
        var candidates = new IBattleUnit[allPawns.Count];
        var targets = new Dictionary<ushort, IBattleUnit>(allPawns.Count);
        for (int i = 0; i < allPawns.Count; i++) {
            candidates[i] = allPawns[i];
            targets[allPawns[i].Id] = allPawns[i];
        }

        var scene = new BattleScene(candidates, targets);

        foreach (var enemy in enemies) {
            if (enemy.Health.Value <= 0f)
                continue;
            if (enemy.Intelligence is not { } intelligence)
                continue;

            var decision = intelligence.Decide(enemy, scene, _relations);
            Apply(enemy, decision, scene);
        }
    }

    /// <summary>把 AI 决策映射为载体动作：停止、逼近或发起施法。</summary>
    private void Apply(UnitPawn enemy, EnemyDecision decision, IBattleScene scene) {
        switch (decision.Kind) {
            case EnemyDecisionKind.Idle:
                enemy.SetMovementInput(Vector2.Zero);
                break;

            case EnemyDecisionKind.MoveTo:
                enemy.SetMovementInput(decision.MoveDirection);
                _battleEngine.OnUnitMoved(enemy, decision.MoveDirection);
                break;

            case EnemyDecisionKind.CastSkill:
                enemy.SetMovementInput(Vector2.Zero);
                TryBeginCast(enemy, decision, scene);
                break;

            default:
                _logger.LogError("Unknown enemy decision kind.");
                break;
        }
    }

    /// <summary>按技能目标类型解析单位目标后发起读条；目标丢失或校验失败仅记日志，下一帧重新决策。</summary>
    private void TryBeginCast(UnitPawn enemy, EnemyDecision decision, IBattleScene scene) {
        var skill = ((IBattleUnit)enemy).GetSkill(decision.SkillId);
        IBattleUnit? target = null;
        if (skill == null)
            return;
        if (skill.NeedUnitTarget) {
            var targetUnit = scene.FindUnit(decision.TargetNetId);
            if (targetUnit == null)
                return;
            target = targetUnit;
        }

        // 位置目标技能直接使用决策锚点，无需解析单位目标
        if (!_battleEngine.BeginCast(enemy, decision.SkillId, target, decision.TargetPosition)) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[EnemyBrain] {Enemy} cast rejected: {SkillId} on {Target}.",
                    enemy.UnitName.Value, decision.SkillId.Id, target?.UnitName ?? "(position)");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[EnemyBrain] {Enemy} starts casting skill {SkillId} on {Target}.",
                enemy.UnitName.Value, decision.SkillId.Id, target?.UnitName ?? "(position)");
    }
}
