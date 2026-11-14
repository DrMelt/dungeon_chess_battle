namespace DungeonChessBattle.Lobby.Shared;

/// <summary>
/// 房间状态，招募板使用。
/// </summary>
public enum RoomStatus : byte {
    /// <summary>等待中。</summary>
    Waiting = 0,
    /// <summary>进行中。</summary>
    InProgress = 1,
    /// <summary>已结束。</summary>
    Finished = 2,
}
