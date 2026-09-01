using DungeonChessBattle.Battle.Entities;
using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 聚合单个玩家在房间内的所有关联状态。
/// 将原本散落在 BattleRoomServer 中的 7 个独立字典统一为一个聚合对象，
/// 通过 playerId 索引的 ConcurrentDictionary 实现线程安全访问。
/// </summary>
internal sealed class PlayerSession(string playerId, string playerName) {
    /// <summary>客户端持久标识，GUID，不对外暴露。</summary>
    public string PlayerId {
        get;
    } = playerId;

    /// <summary>玩家显示名。</summary>
    public string PlayerName {
        get; set;
    } = playerName;

    /// <summary>当前关联的 LiteNetLib peer Id，0 表示未连接。</summary>
    public int PeerId {
        get; set;
    }

    /// <summary>当前是否有活跃网络连接。连接建立时置 PeerId，断开时清 0。</summary>
    public bool IsConnected => PeerId != 0;

    /// <summary>LES 网络玩家句柄。</summary>
    public NetPlayer? NetPlayer {
        get; set;
    }

    /// <summary>玩家输入控制器，由 LES 框架管理。</summary>
    public UnitController? Controller {
        get; set;
    }
}
