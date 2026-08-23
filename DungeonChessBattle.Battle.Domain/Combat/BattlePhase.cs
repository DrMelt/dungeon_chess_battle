namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 战斗阶段，战斗世界自持权威状态，网络载体以字节值投影。
/// </summary>
public enum BattlePhase : byte {
    /// <summary>等待开始，大厅到战斗的过渡。</summary>
    Waiting = 0,
    /// <summary>战斗中，实时推进。</summary>
    Running = 1,
    /// <summary>战斗结束。</summary>
    Finished = 2,
}
