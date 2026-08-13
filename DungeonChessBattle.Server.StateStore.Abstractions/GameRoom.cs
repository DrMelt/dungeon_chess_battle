using DungeonChessBattle.Battle.Domain.Enums;

namespace DungeonChessBattle.Server.StateStore.Abstractions;

/// <summary>
/// 游戏房间数据模型，服务端权威存储；客户端不直接引用，只读视图经 Protocol 的 RoomListing 与 RoomSnapshot 传输。
/// 承载招募板信息与战斗开关，不含战斗单位，单位状态由战斗编排 BattleRoom 面向 IBattleUnit 权威持有。
/// 边界约定：招募板字段，Title、DungeonName、Description、MaxPlayers、CurrentPlayers、Password、Status，
/// 由服务端 Store 层 IGameStateStore 读写；战斗字段 IsActive
/// 由战斗编排 BattleRoom 独占所有权。双方不交叉修改，仅靠约定约束。
/// </summary>
public class GameRoom(string roomId) {
    /// <summary>房间唯一 ID。</summary>
    public string RoomId {
        get;
    } = roomId;

    // ─── 招募板字段，Store 层所有权 ───

    /// <summary>招募板展示的房间标题。</summary>
    public string Title {
        get; set;
    } = string.Empty;

    /// <summary>副本名。</summary>
    public string DungeonName {
        get; set;
    } = string.Empty;

    /// <summary>选中的副本键，服务端据此解析敌人生成配置。</summary>
    public string DungeonKey {
        get; set;
    } = string.Empty;

    /// <summary>招募板展示的房间描述。</summary>
    public string Description {
        get; set;
    } = string.Empty;

    /// <summary>房主玩家名。</summary>
    public string HostName {
        get; set;
    } = string.Empty;

    /// <summary>房间最大玩家数。</summary>
    public int MaxPlayers {
        get; set;
    } = 2;

    /// <summary>房间当前玩家数。</summary>
    public int CurrentPlayers {
        get; set;
    }

    /// <summary>房间密码，空表示无密码。</summary>
    public string? Password {
        get; set;
    }

    /// <summary>房间是否设置了密码。</summary>
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    /// <summary>
    /// 房间创建时间，UTC，服务端权威。
    /// 服务端 Store 层在创建房间时初始化为权威时刻；战斗房间服务器 BattleRoomServer
    /// 从本字段读取权威创建时间注入战斗时钟，客户端经 BattleRoomEntity.CreatedUnixTime 同步。
    /// </summary>
    public DateTime CreatedAt {
        get; set;
    } = DateTime.UtcNow;

    /// <summary>房间状态，等待、进行中或已结束。</summary>
    public RoomStatus Status {
        get; set;
    } = RoomStatus.Waiting;

    /// <summary>战斗是否进行中。</summary>
    /// <remarks>战斗单位状态由战斗编排 BattleRoom 面向 IBattleUnit 的 Pawn 实体权威持有，不再挂载于房间模型。</remarks>
    public bool IsActive {
        get; set;
    } = true;
}
