namespace DungeonChessBattle.Server.StateStore.Abstractions;

/// <summary>
/// 玩家状态存储接口：房间内玩家归属、准备状态与准备单位的选择记录。
/// 以 connectionId 为连接身份键，以 displayName 为房间成员键。
/// 并发语义：任何线程都可安全调用；同一房间内的读改写由实现保证原子性。
/// </summary>
public interface IPlayerStateStore {
    /// <summary>设置房间房主，并将房主登记为房间成员（默认未准备）。</summary>
    void SetRoomHost(string roomId, string hostName);

    /// <summary>登记玩家为房间准备阶段成员（默认未准备），并记录连接归属。</summary>
    void RegisterRoomPlayer(string roomId, string playerName, string playerId, string connectionId);

    /// <summary>登记房间内玩家的 playerId（用于战斗启动时注册白名单）。</summary>
    void RegisterRoomPlayerId(string roomId, string playerName, string playerId);

    /// <summary>获取房间内所有玩家的 (玩家名 → playerId) 映射快照。</summary>
    Dictionary<string, string> GetRoomPlayerIds(string roomId);

    /// <summary>设置房间内玩家准备状态（仅限非房主；房主身份不参与准备判定）。</summary>
    void SetPlayerReady(string roomId, string playerName, bool ready);

    /// <summary>判断房间内除房主外的所有玩家是否都已准备。无其他成员时视为已满足。</summary>
    bool IsAllOthersReady(string roomId);

    /// <summary>获取房间准备状态快照。</summary>
    RoomStateSnapshot GetRoomState(string roomId);

    /// <summary>判断指定连接是否为指定房间的房主（基于连接归属表）。</summary>
    bool IsConnectionRoomHost(string connectionId, string roomId);

    /// <summary>判断指定连接是否为指定房间的成员（基于连接归属表）。</summary>
    bool IsConnectionInRoom(string connectionId, string roomId);

    /// <summary>解析指定连接在房间内登记的玩家名（服务器权威身份）。</summary>
    string? GetPlayerNameForConnection(string connectionId);

    /// <summary>移除房间内玩家（连接断开清理），返回所属房间 ID；未登记时返回 null。</summary>
    string? RemovePlayerByConnection(string connectionId);

    /// <summary>在大厅准备阶段添加单位。</summary>
    bool AddPrepareUnit(string roomId, string unitName, string camp, string playerName, string playerId);

    /// <summary>在大厅准备阶段移除单位。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitName">单位名称。</param>
    /// <param name="camp">阵营。</param>
    /// <param name="ownerName">单位归属玩家名（服务端权威），仅归属者可移除。</param>
    bool RemovePrepareUnit(string roomId, string unitName, string camp, string ownerName);

    /// <summary>获取准备阶段单位列表。</summary>
    IReadOnlyList<UnitSelection> GetPrepareUnits(string roomId);
}
