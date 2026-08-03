using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 轻量房间列表模型，用于招募板列表展示和网络传输。
/// 只包含列表展示的必要字段，避免传输完整房间数据。
/// </summary>
public class RoomListing {
    public string RoomId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public RoomCategory Category { get; init; }
    public string HostName { get; init; } = string.Empty;
    public int CurrentPlayers { get; init; }
    public int MaxPlayers { get; init; }
    public bool HasPassword { get; init; }
    public RoomStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }

    public static RoomListing FromGameRoom(GameRoom room) {
        return new RoomListing {
            RoomId = room.RoomId,
            Title = room.Title,
            Category = room.Category,
            HostName = room.HostName,
            CurrentPlayers = room.CurrentPlayers,
            MaxPlayers = room.MaxPlayers,
            HasPassword = room.HasPassword,
            Status = room.Status,
            CreatedAt = room.CreatedAt,
        };
    }
}