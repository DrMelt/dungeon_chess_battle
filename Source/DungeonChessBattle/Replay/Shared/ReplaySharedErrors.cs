using ErrorOr;

namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// 回放命令映射的可预期失败：玩家数超出移动轨道容量。
/// 描述面向日志与界面提示，自带定位所需的数值。
/// </summary>
public static class ReplaySharedErrors {
    /// <summary>玩家数超出移动轨道容量，轨道序号无法降到轨道键宽度。</summary>
    public static Error MoveTrackOverCapacity(int playerCount, int capacity) => Error.Validation(
        code: "ReplayShared.MoveTrack.OverCapacity",
        description: $"玩家数 {playerCount} 超出移动轨道容量 {capacity}");
}
