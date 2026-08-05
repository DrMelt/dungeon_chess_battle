using DungeonChessBattle.Entities;
using LiteEntitySystem;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 聚合单个玩家在房间内的所有关联状态。
/// 将原本散落在 BattleRoomServer 中的 7 个独立字典统一为一个聚合对象，
/// 通过 playerId 索引的 ConcurrentDictionary 实现线程安全访问。
/// </summary>
internal sealed class PlayerSession(string playerId, string playerName) {
    /// <summary>客户端持久标识（GUID，不对外暴露）</summary>
    public string PlayerId {
        get;
    } = playerId;

    /// <summary>玩家显示名</summary>
    public string PlayerName {
        get; set;
    } = playerName;

    /// <summary>当前关联的 LiteNetLib peer Id（0 表示未连接）</summary>
    public int PeerId {
        get; set;
    }

    /// <summary>房间内玩家 Entity（通过 LES SyncVar 同步到客户端）</summary>
    public PlayerRoomEntity? Entity {
        get; set;
    }

    /// <summary>LES 网络玩家句柄</summary>
    public NetPlayer? NetPlayer {
        get; set;
    }

    /// <summary>玩家输入控制器（预留，由 LES 框架管理）</summary>
    public UnitController? Controller {
        get; set;
    }

    /// <summary>断连时间戳（null 表示当前已连接）</summary>
    public DateTime? DisconnectTime {
        get; set;
    }
}
