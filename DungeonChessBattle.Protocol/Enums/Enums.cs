namespace DungeonChessBattle.Protocol.Enums;

/// <summary>
/// 玩家连接状态，用于断线重连。
/// </summary>
public enum PlayerConnectionState : byte {
    /// <summary>已连接。</summary>
    Connected = 0,
    /// <summary>已断开。</summary>
    Disconnected = 1,
}

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
