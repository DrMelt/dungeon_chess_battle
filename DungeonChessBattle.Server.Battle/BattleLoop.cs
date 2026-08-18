using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Domain.Intelligence;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Entities;
using LiteEntitySystem;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 战斗循环的 LES LocalSingleton 载体，把 AI 决策与战斗推进收编进
/// EntityManager 的逻辑 tick 生命周期，与实体同步严格 1:1。
/// Update 在 OnLogicTick 之前执行，注入 AI 输入使其先于单位位移结算；
/// LateUpdate 在实体更新之后、状态包发送之前执行，战斗变更本 tick 同步。
/// LocalSingleton 不参与预测回滚：回滚只重放实体的 Update()。
/// 本类只做决策到载体动作的映射与战斗推进编排，禁止写实体确定性状态。
/// </summary>
internal sealed class BattleLoop(
    BattleEngine battleEngine,
    CampRelationResolver relations,
    ILogger<BattleLoop> logger,
    IReadOnlyList<UnitPawn> enemies,
    IReadOnlyList<UnitPawn> allPawns,
    Action<IDomainEvent> onDomainEvent) : ILocalSingletonWithUpdate {
    private readonly BattleEngine _battleEngine = battleEngine;
    private readonly CampRelationResolver _relations = relations;
    private readonly ILogger<BattleLoop> _logger = logger;
    private readonly IReadOnlyList<UnitPawn> _enemies = enemies;
    private readonly IReadOnlyList<UnitPawn> _allPawns = allPawns;
    private readonly Action<IDomainEvent> _onDomainEvent = onDomainEvent;

    /// <summary>
    /// 每个逻辑 tick 在 OnLogicTick 之前执行：为全部存活敌人决策并映射为移动输入与施法请求。
    /// 决策由各单位注入的 <see cref="IUnitIntelligence"/> 提供，本方法只做存活调度与决策到载体动作的映射；
    /// 移动与施法执行经 <see cref="BattleEngine"/> 与玩家侧共用同一权威入口。
    /// </summary>
    public void Update(float dt) {
        if (_battleEngine.CurrentPhase != BattlePhase.Running)
            return;
        if (_enemies.Count == 0 || _allPawns.Count == 0)
            return;

        // 决策场景每帧组装一次，候选池与目标索引供全部敌人共享
        var candidates = new IBattleUnit[_allPawns.Count];
        var targets = new Dictionary<ushort, IBattleUnit>(_allPawns.Count);
        for (int i = 0; i < _allPawns.Count; i++) {
            candidates[i] = _allPawns[i];
            targets[_allPawns[i].Id] = _allPawns[i];
        }

        var scene = new BattleScene(candidates, targets);

        foreach (var enemy in _enemies) {
            if (enemy.Health.Value <= 0f)
                continue;
            if (enemy.Intelligence is not { } intelligence)
                continue;

            var decision = intelligence.Decide(enemy, scene, _relations);
            Apply(enemy, decision, scene);
        }
    }

    /// <summary>每个逻辑 tick 在实体更新后、发送前执行：战斗推进并翻译领域事件。</summary>
    public void LateUpdate(float dt) {
        foreach (var e in _battleEngine.Tick(dt))
            _onDomainEvent(e);
    }

    /// <summary>渲染帧回调，服务器端无渲染，留空。</summary>
    public void VisualUpdate(float dt) {
    }

    /// <summary>LocalSingleton 随房间对象释放，无需显式清理。</summary>
    public void Destroy() {
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
                _logger.LogError("[BattleLoop] Unknown enemy decision kind: {Enemy} -> {Kind}.",
                    enemy.UnitName.Value, decision.Kind);
                break;
        }
    }

    /// <summary>按技能目标类型解析单位目标后发起读条；目标丢失或校验失败仅记日志，下一帧重新决策。</summary>
    private void TryBeginCast(UnitPawn enemy, EnemyDecision decision, IBattleScene scene) {
        var skill = ((IBattleUnit)enemy).GetSkill(decision.SkillId);
        IBattleUnit? target = null;
        if (skill == null) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[BattleLoop] {Enemy} cannot find skill {SkillId}.", enemy.UnitName.Value, decision.SkillId.Id);
            return;
        }
        if (skill.NeedUnitTarget) {
            var targetUnit = scene.FindUnit(decision.TargetNetId);
            if (targetUnit == null)
                return;
            target = targetUnit;
        }

        // 位置目标技能直接使用决策锚点，无需解析单位目标
        if (!_battleEngine.BeginCast(enemy, decision.SkillId, target, decision.TargetPosition)) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[BattleLoop] {Enemy} cast rejected: {SkillId} on {Target}.",
                    enemy.UnitName.Value, decision.SkillId.Id, target?.UnitName ?? "(position)");
            return;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[BattleLoop] {Enemy} starts casting skill {SkillId} on {Target}.",
                enemy.UnitName.Value, decision.SkillId.Id, target?.UnitName ?? "(position)");
    }
}
