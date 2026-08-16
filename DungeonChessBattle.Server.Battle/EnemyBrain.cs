using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Ai;
using DungeonChessBattle.Entities;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 敌人大脑编排壳：服务端每 tick 为存活敌人做决策并映射为移动输入与施法请求。
/// 决策全部委托 <see cref="EnemyIntelligence"/>，本类只保留线程语义与网络载体读写；
/// 目标选择、射程与技能选择等规则在 Logic 层，可由假载体独立测试。
/// </summary>
/// <param name="battleEngine">本房间的战斗编排实例，施法与移动打断读条入口。</param>
/// <param name="logger">日志器。</param>
/// <param name="intelligence">敌人 AI 决策模块。</param>
public sealed class EnemyBrain(
    BattleEngine battleEngine,
    ILogger<EnemyBrain> logger,
    EnemyIntelligence intelligence) {
    private readonly ILogger<EnemyBrain> _logger = logger;

    /// <summary>战斗编排引用，驱动移动与读条入口。</summary>
    private readonly BattleEngine _battleEngine = battleEngine;

    /// <summary>敌人 AI 决策模块，本类只做决策到载体输入的映射。</summary>
    private readonly EnemyIntelligence _intelligence = intelligence;

    /// <summary>
    /// 服务端每逻辑 tick 调用，驱动全部存活敌人。仅房间线程调用。
    /// 必须在 EntityManager.Update() 之前执行，确保移动输入先于单位位移结算写入。
    /// </summary>
    /// <param name="enemies">本房间的敌人 Pawn 列表。</param>
    /// <param name="allPawns">本房间全部 Pawn，用作决策候选池。</param>
    public void Tick(IReadOnlyList<UnitPawn> enemies, IReadOnlyList<UnitPawn> allPawns) {
        if (enemies.Count == 0 || allPawns.Count == 0)
            return;

        // 候选池按帧转换一次，供全部敌人决策共享
        var candidates = new IBattleUnit[allPawns.Count];
        for (int i = 0; i < allPawns.Count; i++)
            candidates[i] = allPawns[i];

        for (int i = enemies.Count - 1; i >= 0; i--) {
            var enemy = enemies[i];
            if (enemy.Health.Value <= 0f)
                continue;
            var decision = _intelligence.Decide(enemy, candidates);
            Apply(enemy, decision, allPawns);
        }
    }

    /// <summary>把 AI 决策映射为载体动作：停止、逼近或发起施法。</summary>
    private void Apply(UnitPawn enemy, EnemyDecision decision, IReadOnlyList<UnitPawn> allPawns) {
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
                TryBeginCast(enemy, decision, allPawns);
                break;

            default:
                _logger.LogError("Unknown enemy decision kind.");
                break;
        }
    }

    /// <summary>按技能目标类型解析单位目标后发起读条；目标丢失或校验失败仅记日志。</summary>
    private void TryBeginCast(UnitPawn enemy, EnemyDecision decision, IReadOnlyList<UnitPawn> allPawns) {
        var skill = ((IBattleUnit)enemy).GetSkill(decision.SkillId);
        IBattleUnit? target = null;
        if (skill == null)
            return;
        if (skill.NeedUnitTarget) {
            target = FindTarget(allPawns, decision.TargetNetId);
            if (target == null)
                return;
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

    /// <summary>按网络 ID 在本房间 Pawn 中查找单位目标。</summary>
    private static IBattleUnit? FindTarget(IReadOnlyList<UnitPawn> allPawns, ushort targetNetId) {
        foreach (var pawn in allPawns) {
            if (pawn.Id == targetNetId)
                return pawn;
        }
        return null;
    }
}
