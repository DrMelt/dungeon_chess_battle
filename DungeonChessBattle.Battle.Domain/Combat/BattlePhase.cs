namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 战斗阶段，编排层权威状态。由 BattleRoom 持有并推进，
/// 网络载体 BattleRoomEntity 仅以字节值投影该枚举。
/// </summary>
public enum BattlePhase : byte {
    /// <summary>等待开始，大厅到战斗的过渡。</summary>
    Waiting = 0,
    /// <summary>战斗中，实时推进。</summary>
    Running = 1,
    /// <summary>战斗结束。</summary>
    Finished = 2,
}
