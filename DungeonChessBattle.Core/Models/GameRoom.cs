using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 游戏房间数据模型，承载两个阵营的单位列表与战斗状态及招募板信息。
/// </summary>
public class GameRoom(string roomId) {
    /// <summary>房间唯一 ID。</summary>
    public string RoomId {
        get;
    } = roomId;

    /// <summary>招募板展示的房间标题。</summary>
    public string Title {
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

    /// <summary>房间创建时间（UTC）。</summary>
    public DateTime CreatedAt {
        get; init;
    } = DateTime.UtcNow;

    /// <summary>房间状态（等待/进行中/已结束）。</summary>
    public RoomStatus Status {
        get; set;
    } = RoomStatus.Waiting;

    /// <summary>A 方单位列表（战斗）。</summary>
    public List<IUnitState> UnitsA { get; } = [];

    /// <summary>B 方单位列表（战斗）。</summary>
    public List<IUnitState> UnitsB { get; } = [];

    /// <summary>战斗是否进行中。</summary>
    public bool IsActive {
        get; set;
    } = true;
}
