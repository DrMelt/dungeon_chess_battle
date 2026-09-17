using ErrorOr;

namespace DungeonChessBattle.Replay;

/// <summary>
/// 回放构建的可预期失败：归档不合规范，或归档引用的内容与当前装配不符。
/// 描述面向日志与界面提示，自带定位所需的键与值，不携带异常对象。
/// </summary>
public static class ReplayErrors {
    /// <summary>归档声明的 tick 率非法。</summary>
    public static Error InvalidTickRate(int tickRate) => Error.Validation(
        code: "Replay.TickRate.Invalid", description: $"回放 tick 率非法：{tickRate}");

    /// <summary>归档的内容修订号与当前内容不符。</summary>
    public static Error ContentMismatch(string recorded, string current) => Error.Conflict(
        code: "Replay.Content.Mismatch",
        description: $"回放内容不符：记录 {recorded}，当前 {current}");

    /// <summary>归档的逻辑修订号与当前引擎不符。</summary>
    public static Error LogicMismatch(string recorded, string current) => Error.Conflict(
        code: "Replay.Logic.Mismatch",
        description: $"回放逻辑不符：记录 {recorded}，当前 {current}");

    /// <summary>归档的副本键不合值对象约束。</summary>
    public static Error InvalidDungeonKey(string? dungeonKey) => Error.Validation(
        code: "Replay.DungeonKey.Invalid", description: $"回放副本键非法：{dungeonKey}");

    /// <summary>归档引用的副本未注册。</summary>
    public static Error UnknownDungeonKey(string? dungeonKey) => Error.NotFound(
        code: "Replay.DungeonKey.Unknown", description: $"回放引用的副本未注册：{dungeonKey}");

    /// <summary>归档的单位初始态引用了未注册的单位配置。</summary>
    public static Error UnknownUnitConfig(string unitConfigKey) => Error.NotFound(
        code: "Replay.UnitConfig.Unknown", description: $"回放引用的单位未注册：{unitConfigKey}");

    /// <summary>归档玩家表超出移动轨道容量。</summary>
    public static Error PlayerTableOverCapacity(int playerCount, int capacity) => Error.Validation(
        code: "Replay.Players.OverCapacity",
        description: $"回放玩家表 {playerCount} 人，超出移动轨道容量 {capacity}");

    /// <summary>移动轨道的玩家序号越出玩家表。</summary>
    public static Error MoveTrackIndexOutOfRange(int playerIndex) => Error.Validation(
        code: "Replay.MoveTrack.IndexOutOfRange", description: $"移动轨道玩家序号 {playerIndex} 越出玩家表");

    /// <summary>同一玩家序号出现多条移动轨道。</summary>
    public static Error DuplicateMoveTrack(int playerIndex) => Error.Validation(
        code: "Replay.MoveTrack.Duplicate", description: $"玩家序号 {playerIndex} 的移动轨道重复");
}
