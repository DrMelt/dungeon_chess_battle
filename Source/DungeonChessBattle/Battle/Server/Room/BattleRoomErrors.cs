using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Session.Shared;
using ErrorOr;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 战斗房间启动的可预期失败：房间引用的副本不在场，或房间线程首帧初始化未完成。
/// 描述面向日志与大厅应答，自带房间 ID 与定位所需的键，不携带异常对象；交界异常的现场原因在房间线程日志。
/// </summary>
public static class BattleRoomErrors {
    /// <summary>房间引用的副本未注册。</summary>
    public static Error UnknownDungeon(RoomId roomId, string? dungeonKey) => Error.NotFound(
        code: "BattleRoom.Dungeon.Unknown",
        description: $"房间 '{roomId}' 引用的副本未注册：{dungeonKey}");

    /// <summary>房间线程首帧初始化超时。</summary>
    public static Error InitializeTimeout(RoomId roomId, int timeoutSeconds) => Error.Failure(
        code: "BattleRoom.Initialize.Timeout",
        description: $"房间 '{roomId}' 初始化超时 {timeoutSeconds} 秒");

    /// <summary>房间线程首帧初始化未完成，原因取自房间线程报出的失败。</summary>
    public static Error InitializeFailed(RoomId roomId, string reason) => Error.Failure(
        code: "BattleRoom.Initialize.Failed",
        description: $"房间 '{roomId}' 初始化失败：{reason}");

    /// <summary>房间线程首帧初始化被 LES 或 CLR 交界的异常打断，原因取异常消息。</summary>
    public static Error InitializeInterrupted(string reason) => Error.Failure(
        code: "BattleRoom.Initialize.Interrupted",
        description: $"初始化被交界异常打断：{reason}");

    /// <summary>副本配置引用的单位配置未注册。</summary>
    public static Error UnknownUnitConfig(string dungeonKey, string unitConfigKey) => Error.NotFound(
        code: "BattleRoom.UnitConfig.Unknown",
        description: $"副本 '{dungeonKey}' 引用的单位配置未注册：{unitConfigKey}");

    /// <summary>准备记录提交的阵营选项未在副本配置中声明。</summary>
    public static Error UnknownCampOption(string dungeonKey, string? optionKey) => Error.NotFound(
        code: "BattleRoom.CampOption.Unknown",
        description: $"副本 '{dungeonKey}' 未声明阵营选项：{optionKey}");

    /// <summary>单位配置给出的阵营列表非法：数量或空值不符序列化槽位约定。</summary>
    public static Error InvalidCamps(string unitConfigKey, int count) => Error.Validation(
        code: "BattleRoom.Camps.Invalid",
        description: $"单位 '{unitConfigKey}' 的阵营列表非法，须为 1..{SyncCampsData.MaxCamps} 个非空阵营标识，实际 {count} 个");

    /// <summary>房间根实体创建失败：LES 实体数量达到上限。</summary>
    public static Error RoomEntityLimitReached(RoomId roomId) => Error.Failure(
        code: "BattleRoom.EntityLimit.Room",
        description: $"房间 '{roomId}' 根实体创建失败：实体数量达到上限");

    /// <summary>单位实体创建失败：LES 实体数量达到上限。</summary>
    public static Error UnitEntityLimitReached(RoomId roomId, string unitConfigKey) => Error.Failure(
        code: "BattleRoom.EntityLimit.Unit",
        description: $"房间 '{roomId}' 单位 '{unitConfigKey}' 实体创建失败：实体数量达到上限");
}
