using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Protocol.Dtos;

/// <summary>
/// 轻量房间列表模型，用于招募板列表展示和网络传输。
/// 只包含列表展示的必要字段，避免传输完整房间数据。
/// </summary>
public class RoomListing {
    /// <summary>房间唯一 ID。</summary>
    public string RoomId { get; init; } = string.Empty;

    /// <summary>房间标题。</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>副本名。</summary>
    public string DungeonName { get; init; } = string.Empty;

    /// <summary>房间分类。</summary>
    public RoomCategory Category {
        get; init;
    }

    /// <summary>房主玩家名。</summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>房间当前玩家数。</summary>
    public int CurrentPlayers {
        get; init;
    }

    /// <summary>房间最大玩家数。</summary>
    public int MaxPlayers {
        get; init;
    }

    /// <summary>房间是否设置了密码。</summary>
    public bool HasPassword {
        get; init;
    }

    /// <summary>房间状态。</summary>
    public RoomStatus Status {
        get; init;
    }

    /// <summary>房间创建时间（UTC）。</summary>
    public DateTime CreatedAt {
        get; init;
    }

    /// <summary>
    /// 从完整的房间数据模型转换为轻量的列表展示模型。
    /// </summary>
    /// <param name="room">源房间数据。</param>
    /// <returns>对应的 <see cref="RoomListing"/> 实例。</returns>
    public static RoomListing FromGameRoom(GameRoom room) {
        return new RoomListing {
            RoomId = room.RoomId,
            Title = room.Title,
            DungeonName = room.DungeonName,
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
