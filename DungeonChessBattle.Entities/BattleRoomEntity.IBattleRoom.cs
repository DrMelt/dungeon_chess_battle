using DungeonChessBattle.Battle.Domain;
using BattlePhaseEnum = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;

namespace DungeonChessBattle.Entities;

// BattleRoomEntity 对 IBattleRoom 的适配：把 LES SyncVar/SyncString 映射为领域读写通道。
// 房间级战斗状态权威由本载体承载，战斗世界 BattleScene 直接经接口读写；本文件仅做值映射，无结算逻辑。
// 只读投影显式实现 IReadOnlyBattleRoom；写成员 ProjectBattleStarted/ProjectBattleEnded 由实体类内方法隐式实现，此处不重复。
public partial class BattleRoomEntity : IBattleRoom {
    /// <inheritdoc />
    string IReadOnlyBattleRoom.RoomId => RoomId.Value;

    /// <inheritdoc />
    BattlePhaseEnum IReadOnlyBattleRoom.CurrentPhase => (BattlePhaseEnum)BattlePhase.Value;

    /// <inheritdoc />
    bool IReadOnlyBattleRoom.IsFinished => IsFinished.Value;

    /// <inheritdoc />
    long IReadOnlyBattleRoom.BattleStartUnixTime => BattleStartUnixTime.Value;

    /// <inheritdoc />
    string IReadOnlyBattleRoom.DungeonKey => DungeonKey.Value;
}
