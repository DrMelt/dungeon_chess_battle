using MessagePack;

namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// 回放记录格式版本，数据模型或编码变化时递增。
/// </summary>
public static class ReplayFormatVersion {
    /// <summary>当前回放记录格式版本。</summary>
    public const int Current = 4;
}

/// <summary>
/// 回放记录头部元数据：房间与玩家初始状态，回放端据此重建战斗世界。
/// StartTick 为战斗开始逻辑帧，NextNetId 为服务端最后一个单位 ID + 1，回放端据此对齐单位 ID；
/// Complete 表示录制是否被条目上限截断；DataVersion 为录制端内容数据修订号，重放端据此校验内容一致。
/// </summary>
[MessagePackObject]
public sealed record ReplayRecordHeader(
    [property: Key(0)] int FormatVersion,
    [property: Key(1)] string RoomId,
    [property: Key(2)] string DungeonKey,
    [property: Key(3)] long StartUnixTime,
    [property: Key(4)] int TickRate,
    [property: Key(5)] IReadOnlyList<ReplayPlayerInfo> Players,
    [property: Key(6)] int StartTick,
    [property: Key(7)] ushort NextNetId,
    [property: Key(8)] bool Complete,
    [property: Key(9)] string DataVersion);

/// <summary>
/// 玩家初始状态，回放端按 PlayerIndex 还原玩家单位。
/// SpawnX/SpawnY 为出生点坐标，Y 对应场景 XZ 平面的 Z 轴；NetId 为服务端分配的单位网络 ID。
/// </summary>
[MessagePackObject]
public sealed record ReplayPlayerInfo(
    [property: Key(0)] string PlayerId,
    [property: Key(1)] string PlayerName,
    [property: Key(2)] string UnitConfigKey,
    [property: Key(3)] string CampOptionKey,
    [property: Key(4)] float SpawnX,
    [property: Key(5)] float SpawnY,
    [property: Key(6)] ushort NetId);

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
/// 施法请求条目：逻辑帧、玩家序号、请求载荷与投递结果。
/// Accepted 表示服务端已投递接管（含入排队槽），不含可施放性结论；投递成功但落地被拒的条目照常记录。
/// </summary>
[MessagePackObject]
public readonly record struct CastSkillRecord(
    [property: Key(0)] int Frame,
    [property: Key(1)] byte PlayerIndex,
    [property: Key(2)] string SkillTypeId,
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
