using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Entities;
using LiteEntitySystem;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 房间战斗逻辑的 LES LocalSingleton 载体，把 AI 决策与战斗推进收编进
/// EntityManager 的逻辑 tick 生命周期，与实体同步严格 1:1。
/// Update 在 OnLogicTick 之前执行，注入 AI 输入使其先于单位位移结算；
/// LateUpdate 在实体更新之后、状态包发送之前执行，战斗变更本 tick 同步。
/// </summary>
internal sealed class RoomLogic(
    EnemyBrain enemyBrain,
    BattleEngine battleEngine,
    IReadOnlyList<UnitPawn> enemies,
    IReadOnlyList<UnitPawn> allPawns,
    Action<IDomainEvent> onDomainEvent) : ILocalSingletonWithUpdate {
    private readonly EnemyBrain _enemyBrain = enemyBrain;
    private readonly BattleEngine _battleEngine = battleEngine;
    private readonly IReadOnlyList<UnitPawn> _enemies = enemies;
    private readonly IReadOnlyList<UnitPawn> _allPawns = allPawns;
    private readonly Action<IDomainEvent> _onDomainEvent = onDomainEvent;

    /// <summary>每个逻辑 tick 在 OnLogicTick 之前执行：AI 决策注入移动输入与施法请求。</summary>
    public void Update(float dt) => _enemyBrain.Tick(_enemies, _allPawns);

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
}
