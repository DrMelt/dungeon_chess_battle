using MessagePack;

namespace DungeonChessBattle.Entities.Replay;

/// <summary>
/// 回放记录格式版本，数据模型或编码变化时递增。
/// </summary>
public static class ReplayFormatVersion {
    /// <summary>当前回放记录格式版本。</summary>
    public const int Current = 1;
}

/// <summary>
/// 回放记录头部元数据：房间与玩家初始状态，回放端据此重建战斗世界。
/// </summary>
[MessagePackObject]
public sealed record ReplayRecordHeader(
    [property: Key(0)] int FormatVersion,
    [property: Key(1)] string RoomId,
    [property: Key(2)] string DungeonKey,
    [property: Key(3)] long StartUnixTime,
    [property: Key(4)] int TickRate,
    [property: Key(5)] IReadOnlyList<ReplayPlayerInfo> Players);

/// <summary>
/// 玩家初始状态，回放端按 PlayerIndex 还原玩家单位。
/// SpawnX/SpawnY 为出生点坐标，Y 对应场景 XZ 平面的 Z 轴。
/// </summary>
[MessagePackObject]
public sealed record ReplayPlayerInfo(
    [property: Key(0)] string PlayerId,
    [property: Key(1)] string PlayerName,
    [property: Key(2)] string UnitConfigKey,
    [property: Key(3)] string CampOptionKey,
    [property: Key(4)] float SpawnX,
    [property: Key(5)] float SpawnY);

/// <summary>
/// 移动输入条目：逻辑帧、玩家序号与移动方向。
/// </summary>
[MessagePackObject]
public readonly record struct MoveInputRecord(
    [property: Key(0)] int Frame,
    [property: Key(1)] byte PlayerIndex,
    [property: Key(2)] float MoveX,
    [property: Key(3)] float MoveY);

/// <summary>
/// 施法请求条目：逻辑帧、玩家序号、请求载荷与服务端接受结果。
/// </summary>
[MessagePackObject]
public readonly record struct CastSkillRecord(
    [property: Key(0)] int Frame,
    [property: Key(1)] byte PlayerIndex,
    [property: Key(2)] ushort SkillTypeId,
    [property: Key(3)] ushort TargetNetId,
    [property: Key(4)] float TargetPosX,
    [property: Key(5)] float TargetPosZ,
    [property: Key(6)] bool Accepted);

/// <summary>
/// 聚焦目标请求条目：逻辑帧、玩家序号、目标网络 ID 与服务端接受结果。
/// </summary>
[MessagePackObject]
public readonly record struct FocusTargetRecord(
    [property: Key(0)] int Frame,
    [property: Key(1)] byte PlayerIndex,
    [property: Key(2)] ushort TargetNetId,
    [property: Key(3)] bool Accepted);

/// <summary>回放记录只读快照，服务端导出与客户端下载解析共用。</summary>
[MessagePackObject]
public sealed record ReplayRecordSnapshot(
    [property: Key(0)] ReplayRecordHeader Header,
    [property: Key(1)] IReadOnlyList<MoveInputRecord> MoveInputs,
    [property: Key(2)] IReadOnlyList<CastSkillRecord> CastSkills,
    [property: Key(3)] IReadOnlyList<FocusTargetRecord> FocusTargets);
