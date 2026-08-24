using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared;

/// <summary>
/// 战斗世界状态投影器：把战斗世界自持的单位状态与阶段投影给外部载体。
/// 服务端实现为写 LES SyncVar；BattleScene 在阶段变化与 Tick 末尾调用。
/// 依赖只读状态契约，不触碰具体实体。
/// </summary>
public interface IBattleProjector {
    /// <summary>投影全部单位状态与当前阶段。单位数有限，全量投影即可，载体按需去重。</summary>
    void Project(IReadOnlyList<IProjectableBattleState> units, BattlePhase phase);
}
