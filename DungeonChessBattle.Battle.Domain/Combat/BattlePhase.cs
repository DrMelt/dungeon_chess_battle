namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 战斗阶段（编排层权威状态）。由 BattleRoom 持有并推进，
/// 网络载体（BattleRoomEntity）仅以字节值投影该枚举。
/// </summary>
public enum BattlePhase : byte {
    /// <summary>等待开始（大厅→战斗过渡）。</summary>
    Waiting = 0,
    /// <summary>战斗中（实时 Tick）。</summary>
    Running = 1,
    /// <summary>战斗结束。</summary>
    Finished = 2,
}
