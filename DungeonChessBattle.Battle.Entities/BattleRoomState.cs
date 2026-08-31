using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 房间级战斗状态只读投影：反映 <see cref="BattleRoomEntity"/> 的同步字段。
/// 客户端本地世界与 UI 统一取数，隔离 LES SyncVar 类型；仅依赖 Battle.Shared。
/// </summary>
/// <param name="RoomId">房间唯一 ID。</param>
/// <param name="DungeonKey">选中的副本键，客户端据此加载环境场景。</param>
/// <param name="Phase">战斗阶段。</param>
/// <param name="BattleStartUnixTime">战斗开始时间，UTC Unix 秒；房间实体未同步时为 null，已同步未开战时为 0。</param>
public readonly record struct BattleRoomState(
    string? RoomId,
    string? DungeonKey,
    BattlePhase Phase,
    long? BattleStartUnixTime) {
    /// <summary>战斗是否已结束，由阶段推导，避免与 Phase 冗余两处真相。</summary>
    public bool IsFinished => Phase == BattlePhase.Finished;
}
