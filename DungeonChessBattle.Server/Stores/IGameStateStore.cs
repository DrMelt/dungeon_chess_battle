using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Server.Stores;

/// <summary>
/// 房间状态存储接口：大厅级房间配置、密码与招募板状态的查询与写入。
/// </summary>
public interface IRoomStateStore {
    /// <summary>注册房间（准备阶段）。房间已存在时返回 false。</summary>
    bool TryRegisterRoom(string roomId, string? password, GameRoom config);

    /// <summary>判断房间是否已注册（准备阶段或战斗中）。</summary>
    bool RoomExists(string roomId);

    /// <summary>获取房间配置；不存在时返回 null。</summary>
    GameRoom? GetRoomConfig(string roomId);

    /// <summary>获取所有有效房间的招募板列表，按创建时间倒序排列。</summary>
    IReadOnlyList<RoomListing> ListActiveRooms();

    /// <summary>更新房间的招募板状态。</summary>
    void UpdateRoomStatus(string roomId, RoomStatus status);

    /// <summary>更新房间当前玩家数。</summary>
    void UpdatePlayerCount(string roomId, int count);

    /// <summary>验证房间密码；无密码房间返回 true。</summary>
    bool ValidateRoomPassword(string roomId, string? password);

    /// <summary>移除房间全部状态数据（配置、密码、成员、单位、归属）。</summary>
    void RemoveRoomState(string roomId);

    /// <summary>清空全部状态数据（服务端停止时调用）。</summary>
    void ClearAllState();
}

/// <summary>
/// 玩家状态存储接口：房间内玩家归属、准备状态与准备单位的选择记录。
/// 以 peerId 为连接身份键，以 displayName 为房间成员键。
/// </summary>
public interface IPlayerStateStore {
    /// <summary>设置房间房主，并将房主登记为房间成员（默认未准备）。</summary>
    void SetRoomHost(string roomId, string hostName);

    /// <summary>登记玩家为房间准备阶段成员（默认未准备），并记录 peer 归属。</summary>
    void RegisterRoomPlayer(string roomId, string playerName, string playerId, int peerId);

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

    /// <summary>判断指定 peer 是否为指定房间的房主（基于 peer 归属表）。</summary>
    bool IsPeerRoomHost(int peerId, string roomId);

    /// <summary>解析指定 peer 在房间内登记的玩家名（服务器权威身份）。</summary>
    string? GetPlayerNameForPeer(int peerId);

    /// <summary>移除房间内玩家（peer 断线清理），返回所属房间 ID；未登记时返回 null。</summary>
    string? RemovePlayerByPeer(int peerId);

    /// <summary>在大厅准备阶段添加单位。</summary>
    bool AddPrepareUnit(string roomId, string unitName, string camp, string playerName);

    /// <summary>在大厅准备阶段移除单位。</summary>
    bool RemovePrepareUnit(string roomId, string unitName, string camp);

    /// <summary>获取准备阶段单位列表。</summary>
    IReadOnlyList<UnitSelection> GetPrepareUnits(string roomId);
}

/// <summary>
/// 服务器状态存储门面：组合房间状态与玩家状态子接口。
/// 网络连接密钥（LobbyNetworkServer）与战斗房间白名单（BattleRoomServer）分别
/// 属于网络层与战斗房间的私有所有权，不纳入本门面。
/// </summary>
public interface IGameStateStore : IRoomStateStore, IPlayerStateStore;
