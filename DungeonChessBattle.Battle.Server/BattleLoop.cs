using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 战斗循环的 LES LocalSingleton 适配器，把战斗世界收编进 EntityManager 的逻辑 tick 生命周期，
/// 与实体同步严格 1:1。Update 在 OnLogicTick 之前执行 ApplyDecisions 与预输入重试，
/// AI 输入与排队中的施法意图先于单位位移结算；LateUpdate 在实体更新后、状态包发送前执行 Tick，
/// 战斗变更本 tick 同步。本钩子的 Update/LateUpdate 只在正常 tick 路径执行，不参与预测回滚：
/// 回滚只重放实体的 Update()。
/// 本类只做推进转发与整帧领域事件外送，战斗状态与 AI 决策全部在 <see cref="BattleScene"/> 内。
/// </summary>
internal sealed class BattleLoop(
    BattleScene battleScene,
    CastPreInputBuffer castPreInput,
    Action<BattleScene> battleSyncer,
    Action<IReadOnlyList<IBattleEvent>> battleEventHandler) : ILocalSingletonWithUpdate {
    private readonly BattleScene _battleScene = battleScene;
    private readonly CastPreInputBuffer _castPreInput = castPreInput;
    private readonly Action<BattleScene> _battleSyncer = battleSyncer;
    private readonly Action<IReadOnlyList<IBattleEvent>> _battleEventHandler = battleEventHandler;
    /// <summary>
    /// 每个逻辑 tick 在 OnLogicTick 之前执行：AI 前置推进注入移动输入与施法请求，
    /// 随后推进预输入缓冲。客户端请求与本钩子之后的同帧输入按记录顺序落地，回放须同序。
    /// </summary>
    public void Update(float dt) {
        _battleScene.ApplyDecisions();
        _castPreInput.Advance(dt);
    }

    /// <summary>每个逻辑 tick 在实体更新后、发送前执行：战斗推进 → 状态同步 → 整帧事件一次外送。</summary>
    public void LateUpdate(float dt) {
        var events = _battleScene.Tick(dt);
        _battleSyncer(_battleScene);
        _battleEventHandler(events);
    }

    /// <summary>渲染帧回调，服务器端无渲染，留空。</summary>
    public void VisualUpdate(float dt) {
    }

    /// <summary>LocalSingleton 随房间对象释放，无需显式清理。</summary>
    public void Destroy() {
    }
}
