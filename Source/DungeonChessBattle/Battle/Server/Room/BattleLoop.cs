using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 战斗循环的 LES LocalSingleton 适配器，把战斗世界收编进 EntityManager 的逻辑 tick 生命周期，
/// 与实体同步严格 1:1。Update 在实体更新之前执行，此时玩家输入尚未到达。
/// LateUpdate 在实体更新后、状态包发送前执行：先经输入门面预备本帧意图，再推进战斗，战斗变更本 tick 同步。
/// 意图预备排在实体更新之后，本 tick 到达的玩家输入当帧即生效，与回放端「注入 → 预备 → Tick」同序。
/// 本钩子只在正常 tick 路径执行，不参与预测回滚：回滚只重放实体的 Update()。
/// 本类只做推进转发与整帧领域事件外送，意图产出与战斗结算全在 Battle.Logic 内。
/// </summary>
internal sealed class BattleLoop(
    BattleScene battleScene,
    BattleIntentHub intentHub,
    Action<BattleScene> battleSyncer,
    Action<IReadOnlyList<IBattleEvent>> battleEventHandler) : ILocalSingletonWithUpdate {
    private readonly BattleScene _battleScene = battleScene;
    private readonly BattleIntentHub _intentHub = intentHub;
    private readonly Action<BattleScene> _battleSyncer = battleSyncer;
    private readonly Action<IReadOnlyList<IBattleEvent>> _battleEventHandler = battleEventHandler;
    /// <summary>
    /// 逻辑 tick 起始钩子：本帧意图预备须晚于玩家输入到达，故不在此处而收在 <see cref="LateUpdate"/>。
    /// </summary>
    public void Update(float dt) {
    }

    /// <summary>
    /// 每个逻辑 tick 在实体更新后、发送前执行：预备本帧意图 → 战斗推进 → 状态同步 → 整帧事件一次外送。
    /// 本钩子之前的客户端请求与实体输入只登记意图，裁定在 <see cref="BattleScene.Tick"/> 内单点完成。
    /// </summary>
    public void LateUpdate(float dt) {
        _intentHub.PrepareTick(dt);
        var events = _battleScene.Tick(dt);
        _battleSyncer(_battleScene);
        _battleEventHandler(events);
    }

    /// <summary>渲染帧回调，服务器端无渲染帧。</summary>
    public void VisualUpdate(float dt) {
    }

    /// <summary>LocalSingleton 随房间对象释放，无需显式清理。</summary>
    public void Destroy() {
    }
}
