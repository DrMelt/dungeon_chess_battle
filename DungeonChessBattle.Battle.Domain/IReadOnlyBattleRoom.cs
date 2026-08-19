using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain;

/// <summary>
/// 房间级战斗状态的只读投影通道，客户端与查询侧经此读取服务端权威同步后的房间状态。
/// 由 <see cref="IBattleRoom"/> 继承并追加服务端权威写入口，实现方为 Entities 的 BattleRoomEntity。
/// </summary>
public interface IReadOnlyBattleRoom {
    /// <summary>房间唯一 ID。</summary>
    string RoomId {
        get;
    }

    /// <summary>战斗阶段，权威由载体 BattleRoomEntity 承载。</summary>
    BattlePhase CurrentPhase {
        get;
    }

    /// <summary>战斗是否已结束。</summary>
    bool IsFinished {
        get;
    }

    /// <summary>战斗开始时间，Unix 秒，UTC，服务端权威。</summary>
    long BattleStartUnixTime {
        get;
    }

    /// <summary>房间选中的副本键，客户端据此呈现对应环境场景。</summary>
    string DungeonKey {
        get;
    }
}
