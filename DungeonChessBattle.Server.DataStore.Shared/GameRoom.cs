using DungeonChessBattle.Lobby.Shared;

namespace DungeonChessBattle.Server.DataStore.Shared;

/// <summary>
/// 游戏房间数据模型，服务端权威存储；客户端不直接引用，只读视图经 Protocol 的 RoomListing 与 RoomSnapshot 传输。
/// 承载招募板信息，不含战斗单位，单位状态由战斗世界 BattleScene 面向 BattleUnit 权威持有。
/// 边界约定：招募板字段，Description、MaxPlayers、CurrentPlayers、Password、Status，
/// 由服务端 Store 层 IGameStateStore 读写，双方不交叉修改。
/// </summary>
public class GameRoom(string roomId) {
    /// <summary>房间唯一 ID。</summary>
    public string RoomId {
        get;
    } = roomId;

    // ─── 招募板字段，Store 层所有权 ───

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

    /// <summary>房间创建时间，UTC，服务端权威，大厅列表按此排序与展示。</summary>
    public DateTime CreatedAt {
        get; set;
    } = DateTime.UtcNow;

    /// <summary>房间状态，等待、进行中或已结束。</summary>
    public RoomStatus Status {
        get; set;
    } = RoomStatus.Waiting;

    /// <summary>房间创建时服务端内容指纹（DataRevision），客户端不一致拒绝加入，保证对战双方内容同源。</summary>
    public string ContentFingerprint {
        get; set;
    } = string.Empty;
}
