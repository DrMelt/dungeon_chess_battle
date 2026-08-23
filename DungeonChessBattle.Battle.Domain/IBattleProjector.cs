using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain;

/// <summary>
/// 战斗世界状态投影器：把战斗世界自持的单位状态与阶段投影给外部载体或展示层。
/// 在线端实现为写 LES SyncVar，回放端实现为写回放展示模型；BattleScene 在阶段变化与 Tick 末尾调用。
/// </summary>
public interface IBattleProjector {
    /// <summary>投影全部单位状态与当前阶段。单位数有限，全量投影即可，载体按需去重。</summary>
    void Project(IReadOnlyList<BattleUnit> units, BattlePhase phase);
}
