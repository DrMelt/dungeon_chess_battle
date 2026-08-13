using DungeonChessBattle.Battle.Domain.Enums;

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

    /// <summary>选中的副本键。</summary>
    public string DungeonKey { get; init; } = EntityConstants.DefaultDungeonKey;

    /// <summary>招募板展示的房间描述。</summary>
    public string Description { get; init; } = string.Empty;

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

    /// <summary>房间创建时间，UTC。</summary>
    public DateTime CreatedAt {
        get; init;
    }
}
