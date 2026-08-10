using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 游戏房间数据模型（服务端权威、客户端只读共享）。
/// 承载招募板信息与战斗开关，不含战斗单位（单位状态由 Logic 层权威持有）。
/// 边界约定：招募板字段（Title/DungeonName/Description/Category/MaxPlayers/CurrentPlayers/Password/Status）
/// 由服务端 Store 层（IGameStateStore）读写；战斗字段（Units/IsActive）
/// 由 Logic 层（GameLogicService 单房间门面）独占所有权。双方不交叉修改，仅靠约定约束。
/// </summary>
public class GameRoom(string roomId) {
    /// <summary>房间唯一 ID。</summary>
    public string RoomId {
        get;
    } = roomId;

    // ─── 招募板字段（Store 层所有权） ───

    /// <summary>招募板展示的房间标题。</summary>
    public string Title {
        get; set;
    } = string.Empty;

    /// <summary>副本名。</summary>
    public string DungeonName {
        get; set;
    } = string.Empty;

    /// <summary>招募板展示的房间描述。</summary>
    public string Description {
        get; set;
    } = string.Empty;

    /// <summary>房间分类（休闲/竞技/练习/锦标赛）。</summary>
    public RoomCategory Category {
        get; set;
    } = RoomCategory.Casual;

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

    /// <summary>房间密码（空表示无密码）。</summary>
    public string? Password {
        get; set;
    }

    /// <summary>房间是否设置了密码。</summary>
    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    /// <summary>
    /// 房间创建时间（UTC）。
    /// 服务端 Store 层在创建房间时初始化为权威时刻；
    /// 客户端在进入战斗后从同步字段（BattleRoomEntity.CreatedUnixTime）回填，
    /// 用于跨端一致的战斗计时起点。服务端与客户端均只写一次，不互相覆盖。
    /// </summary>
    public DateTime CreatedAt {
        get; set;
    } = DateTime.UtcNow;

    /// <summary>房间状态（等待/进行中/已结束）。</summary>
    public RoomStatus Status {
        get; set;
    } = RoomStatus.Waiting;

    /// <summary>战斗是否进行中。</summary>
    /// <remarks>战斗单位状态（UnitModel）已由 Logic 层（GameLogicService 单房间门面）权威持有，不再挂载于房间模型。</remarks>
    public bool IsActive {
        get; set;
    } = true;
}
