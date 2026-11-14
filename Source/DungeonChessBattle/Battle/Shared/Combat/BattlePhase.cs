namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗阶段状态标记。
/// </summary>
public enum BattlePhase : byte {
    /// <summary>等待开始。</summary>
    Waiting = 0,
    /// <summary>战斗中。</summary>
    Running = 1,
    /// <summary>战斗结束。</summary>
    Finished = 2,
}
