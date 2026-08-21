using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Events;
using LiteEntitySystem;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// 战斗循环的 LES LocalSingleton 适配器，把战斗世界收编进 EntityManager 的逻辑 tick 生命周期，
/// 与实体同步严格 1:1。Update 在 OnLogicTick 之前执行 ApplyDecisions，AI 输入先于单位位移结算；
/// LateUpdate 在实体更新后、状态包发送前执行 Tick，战斗变更本 tick 同步。
/// LocalSingleton 不参与预测回滚：回滚只重放实体的 Update()。
/// 本类只做推进转发与整帧领域事件外送，战斗状态与 AI 决策全部在 <see cref="IBattleScene"/> 内。
/// </summary>
internal sealed class BattleLoop(
    IBattleScene battleScene,
    Action<IReadOnlyList<IBattleEvent>> battleEventHandler) : ILocalSingletonWithUpdate {
    private readonly IBattleScene _battleScene = battleScene;

    private readonly Action<IReadOnlyList<IBattleEvent>> _battleEventHandler = battleEventHandler;
    /// <summary>每个逻辑 tick 在 OnLogicTick 之前执行：AI 前置推进，注入移动输入与施法请求。</summary>
    public void Update(float dt) {
        _battleScene.ApplyDecisions();
    }

    /// <summary>每个逻辑 tick 在实体更新后、发送前执行：战斗推进并把整帧领域事件一次外送。</summary>
    public void LateUpdate(float dt) {
        _battleEventHandler(_battleScene.Tick(dt));
    }

    /// <summary>渲染帧回调，服务器端无渲染，留空。</summary>
    public void VisualUpdate(float dt) {
    }

    /// <summary>LocalSingleton 随房间对象释放，无需显式清理。</summary>
    public void Destroy() {
    }
}
