namespace DungeonChessBattle.Server.StateStore.Abstractions;

/// <summary>
/// 玩家状态存储接口：房间内玩家归属、准备状态与准备单位的选择记录。
/// 以 connectionId 为连接身份键，以 displayName 为房间成员键。
/// 并发语义：任何线程都可安全调用；同一房间内的读改写由实现保证原子性。
/// </summary>
public interface IPlayerStateStore {
    /// <summary>设置房间房主，并将房主登记为房间成员，默认未准备。</summary>
    void SetRoomHost(string roomId, string hostName);

    /// <summary>登记玩家为房间准备阶段成员，默认未准备，并记录连接归属。</summary>
    void RegisterRoomPlayer(string roomId, string playerName, string playerId, string connectionId);

    /// <summary>登记房间内玩家的 playerId，用于战斗启动时注册白名单。</summary>
    void RegisterRoomPlayerId(string roomId, string playerName, string playerId);

    /// <summary>获取房间内所有玩家的玩家名到 playerId 映射快照。</summary>
    Dictionary<string, string> GetRoomPlayerIds(string roomId);

    /// <summary>设置房间内玩家准备状态，仅限非房主，房主身份不参与准备判定。</summary>
    /// <remarks>未选择角色时拒绝准备，返回是否成功；取消准备不校验角色。</remarks>
    bool TrySetPlayerReady(string roomId, string playerName, bool ready);

    /// <summary>判断房间内指定玩家是否已准备。</summary>
    bool IsPlayerReady(string roomId, string playerName);

    /// <summary>判断房间内所有玩家（含房主）是否都已选择角色。</summary>
    bool AreAllPlayersUnitSelected(string roomId);

    /// <summary>判断房间内除房主外的所有玩家是否都已准备。无其他成员时视为已满足。</summary>
    bool IsAllOthersReady(string roomId);

    /// <summary>获取房间准备状态快照。</summary>
    RoomStateSnapshot GetRoomState(string roomId);

    /// <summary>判断指定连接是否为指定房间的房主，基于连接归属表。</summary>
    bool IsConnectionRoomHost(string connectionId, string roomId);

    /// <summary>判断指定连接是否为指定房间的成员，基于连接归属表。</summary>
    bool IsConnectionInRoom(string connectionId, string roomId);

    /// <summary>解析指定连接所属的房间 ID，基于连接归属表；未归属任何房间时返回 null。</summary>
    string? GetRoomIdForConnection(string connectionId);

    /// <summary>解析指定连接在房间内登记的玩家名，服务器权威身份。</summary>
    string? GetPlayerNameForConnection(string connectionId);

    /// <summary>登记连接为大厅登录会话，玩家名作为服务端权威身份；名字为空或超长时返回 false。</summary>
    bool TryRegisterLoginSession(string connectionId, string playerName);

    /// <summary>解析连接登记的登录名；未登录时返回 null。</summary>
    string? GetLoginPlayerName(string connectionId);

    /// <summary>移除连接的登录会话，连接断开时清理。</summary>
    void RemoveLoginSession(string connectionId);

    /// <summary>按登入名字解析玩家记录主键；名字首次出现时自动登记并分配稳定主键，之后复用。记录主键仅进程生命周期内稳定。</summary>
    string ResolvePlayerRecordId(string playerName);

    /// <summary>移除房间内玩家，连接断开或主动离开清理，返回所属房间 ID；玩家未登记或房间因此被删除时返回 null。</summary>
    /// <remarks>
    /// 对准备阶段，Waiting，房间还执行：减少当前玩家数、移除玩家准备单位；
    /// 房主退出时转让房主给剩余玩家；最后一人退出时删除房间全部状态并返回 null。
    /// 战斗中，InProgress，房间仅做基础清理，生命周期由 BattleRoomManager 负责。
    /// </remarks>
    string? RemovePlayerByConnection(string connectionId);

    /// <summary>在大厅准备阶段添加单位；玩家已准备时返回 false，禁止准备后更改角色。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitConfigKey">单位配置键，与 UnitConfig.ConfigKey 一致。</param>
    /// <param name="campOptionKey">玩家阵营选项键，对应副本配置 PlayerCampOptions 中的选项，阵营由副本配置权威解析。</param>
    /// <param name="playerName">单位归属玩家名，服务端权威。</param>
    /// <param name="playerId">玩家持久标识，控制器绑定用权威键。</param>
    bool AddPrepareUnit(string roomId, string unitConfigKey, string campOptionKey,
        string playerName, string playerId);

    /// <summary>在大厅准备阶段移除单位；玩家已准备时返回 false，禁止准备后更改角色。</summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitConfigKey">单位配置键，与 UnitConfig.ConfigKey 一致。</param>
    /// <param name="ownerName">单位归属玩家名，服务端权威，仅归属者可移除。</param>
    bool RemovePrepareUnit(string roomId, string unitConfigKey, string ownerName);

    /// <summary>获取准备阶段单位列表。</summary>
    IReadOnlyList<UnitSelection> GetPrepareUnits(string roomId);
}
