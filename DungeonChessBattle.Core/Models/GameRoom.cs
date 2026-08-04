using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 游戏房间数据模型，承载两个阵营的单位列表与战斗状态及招募板信息。
/// </summary>
public class GameRoom(string roomId) {
    public string RoomId {
        get;
    } = roomId;

    // --- 招募板字段 ---
    public string Title {
        get; set;
    } = string.Empty;

    public string Description {
        get; set;
    } = string.Empty;

    public RoomCategory Category {
        get; set;
    } = RoomCategory.Casual;

    public string HostName {
        get; set;
    } = string.Empty;

    public int MaxPlayers {
        get; set;
    } = 2;

    public int CurrentPlayers {
        get; set;
    }

    public string? Password {
        get; set;
    }

    public bool HasPassword => !string.IsNullOrWhiteSpace(Password);

    public DateTime CreatedAt {
        get; init;
    } = DateTime.UtcNow;

    public RoomStatus Status {
        get; set;
    } = RoomStatus.Waiting;

    // --- 战斗字段 ---
    public List<IUnitState> UnitsA { get; } = [];
    public List<IUnitState> UnitsB { get; } = [];
    public bool IsActive {
        get; set;
    } = true;
}
