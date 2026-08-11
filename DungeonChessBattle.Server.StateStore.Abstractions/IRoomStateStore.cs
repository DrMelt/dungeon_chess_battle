using DungeonChessBattle.Protocol.Enums;
using DungeonChessBattle.Protocol.Dtos;

namespace DungeonChessBattle.Server.StateStore.Abstractions;

/// <summary>
/// 房间状态存储接口：大厅级房间配置、密码与招募板状态的查询与写入。
/// 并发语义：任何线程都可安全调用；同一房间内的读改写由实现保证原子性。
/// </summary>
public interface IRoomStateStore {
    /// <summary>注册房间（准备阶段）。房间已存在时返回 false。</summary>
    bool TryRegisterRoom(string roomId, string? password, GameRoom config);

    /// <summary>
    /// 组合原子注册房间并登记房主为成员。
    /// 单次调用内完成：注册房间、初始化子表、记录房主、登记房主成员、
    /// 记录房主连接归属与 playerId。房间已存在时返回 false，且不产生任何副作用。
    /// </summary>
    bool TryRegisterRoomWithHost(string roomId, string? password, GameRoom config,
        string hostName, string hostPlayerId, string hostConnectionId);

    /// <summary>判断房间是否已注册（准备阶段或战斗中）。</summary>
    bool RoomExists(string roomId);

    /// <summary>获取房间配置；不存在时返回 null。</summary>
    GameRoom? GetRoomConfig(string roomId);

    /// <summary>判断指定 playerId 是否为指定房间的登记成员（房间存在期间持续有效）。</summary>
    bool IsRoomMember(string roomId, string playerId);

    /// <summary>获取所有有效房间的招募板列表，按创建时间倒序排列。</summary>
    IReadOnlyList<RoomListing> ListActiveRooms();

    /// <summary>更新房间的招募板状态。</summary>
    void UpdateRoomStatus(string roomId, RoomStatus status);

    /// <summary>更新房间当前玩家数。</summary>
    void UpdatePlayerCount(string roomId, int count);

    /// <summary>原子自增房间当前玩家数并返回新值；房间不存在时返回 0。</summary>
    int IncrementPlayerCount(string roomId);

    /// <summary>验证房间密码；无密码房间返回 true。</summary>
    bool ValidateRoomPassword(string roomId, string? password);

    /// <summary>移除房间全部状态数据（配置、密码、成员、单位、归属）。</summary>
    void RemoveRoomState(string roomId);

    /// <summary>清空全部状态数据（服务端停止时调用）。</summary>
    void ClearAllState();
}
