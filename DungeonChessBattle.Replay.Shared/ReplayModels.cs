using MessagePack;

namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// 回放归档元数据，即 <see cref="ReplayChunkType.Meta"/> 块的全部内容，也是列表侧的唯一元数据真相：
/// 服务端摘要、本地缓存条目与协议 DTO 都由本类型投影，不再有第二份形状。
/// 编码进容器头之后本类型不带格式版本，版本门控归 <see cref="ReplayArchive"/>。
/// StartTick 为战斗开始逻辑帧，EndTick 为战斗结束或最后一条输入所在帧，二者 inclusive。
/// DataVersion 为录制端内容数据修订号，LogicVersion 为录制端结算逻辑修订号，重放端据此双重门控。
/// </summary>
[MessagePackObject]
public sealed record ReplayMeta(
    [property: Key(0)] string RoomId,
    [property: Key(1)] string DungeonKey,
    [property: Key(2)] long StartUnixTime,
    [property: Key(3)] int TickRate,
    [property: Key(4)] int StartTick,
    [property: Key(5)] int EndTick,
    [property: Key(6)] string DataVersion,
    [property: Key(7)] string LogicVersion,
    [property: Key(8)] IReadOnlyList<ReplayPlayerInfo> Players) {
    /// <summary>回放覆盖的逻辑帧数，时间轴长度以此为准，不由最后一条输入倒推。</summary>
    [IgnoreMember]
    public int DurationTicks => Math.Max(0, EndTick - StartTick + 1);
}

/// <summary>
/// 参与玩家条目：下标即各输入轨道里的玩家序号。单位初始态与阵营在
/// <see cref="ReplayUnitInit"/> 里按 NetId 收录，本条目只承担列表展示与序号锚定。
/// </summary>
[MessagePackObject]
public sealed record ReplayPlayerInfo(
    [property: Key(0)] string PlayerName,
    [property: Key(1)] string UnitConfigKey,
    [property: Key(2)] ushort NetId);

/// <summary>
/// 单位初始态：世界重建的唯一依据，玩家与敌人同表同序，按录制端创建顺序落盘。
/// NetId 是录制端 LES 同步实体 ID，条目里的目标 ID 与本表同一数轴；重建端不再从副本配置的
/// 生成顺序推演 ID，敌人数组错位这类静默漂移在源头消失。
/// 玩家身份不在本条目：它由 <see cref="ReplayMeta.Players"/> 的 NetId 判定，一份事实不留两个落点。
/// SpawnX/SpawnY 为出生点坐标，Y 对应场景 XZ 平面的 Z 轴。单位属性仍按 UnitConfigKey 取当前配置。
/// </summary>
[MessagePackObject]
public sealed record ReplayUnitInit(
    [property: Key(0)] ushort NetId,
    [property: Key(1)] string UnitConfigKey,
    [property: Key(2)] IReadOnlyList<string> Camps,
    [property: Key(3)] float SpawnX,
    [property: Key(4)] float SpawnY);

/// <summary>
/// 移动输入轨道：一个玩家的连续方向意图段序列，按帧升序。
/// 逐 tick 提交语义不变，收拢只发生在存储侧，读侧展开为逐帧重投。
/// </summary>
[MessagePackObject]
public sealed record ReplayMoveTrack(
    [property: Key(0)] byte PlayerIndex,
    [property: Key(1)] IReadOnlyList<ReplayMoveRun> Runs) {
    /// <summary>可寻址玩家数上限：轨道键是 byte，下标 0..255 容 256 名。录制、成型与重建三处共用这一处口径。</summary>
    public const int MaxPlayers = byte.MaxValue + 1;
}

/// <summary>
/// 方向意图段：自 Frame 起连续 Length 帧提交同一方向，输入断供处即段界。
/// 方向分量保持 bit-exact，不做量化——量化等于换一条确定性斜坡。
/// </summary>
[MessagePackObject]
public readonly record struct ReplayMoveRun(
    [property: Key(0)] int Frame,
    [property: Key(1)] int Length,
    [property: Key(2)] float DirX,
    [property: Key(3)] float DirY) {
    /// <summary>本段覆盖的最后一帧，inclusive。</summary>
    [IgnoreMember]
    public int EndFrame => Frame + Length - 1;
}

/// <summary>施法请求条目：Accepted 表示服务端已投递接管（含入排队槽），不含可施放性结论。</summary>
[MessagePackObject]
public readonly record struct ReplayCastEntry(
    [property: Key(0)] int Frame,
    [property: Key(1)] byte PlayerIndex,
    [property: Key(2)] string SkillKey,
    [property: Key(3)] ushort TargetNetId,
    [property: Key(4)] float TargetPosX,
    [property: Key(5)] float TargetPosZ,
    [property: Key(6)] bool Accepted);

/// <summary>聚焦目标请求条目。</summary>
[MessagePackObject]
public readonly record struct ReplayFocusEntry(
    [property: Key(0)] int Frame,
    [property: Key(1)] byte PlayerIndex,
    [property: Key(2)] ushort TargetNetId,
    [property: Key(3)] bool Accepted);

/// <summary>
/// 一场回放的完整内存表示：编码入容器、解码由此产出，录制端与重放端共用的唯一形状。
/// 轨道与条目序列的有序性由录制端保证，重放端不信任输入、构造时再排一次。
/// </summary>
public sealed record ReplayRecording(
    ReplayMeta Meta,
    IReadOnlyList<ReplayUnitInit> Units,
    IReadOnlyList<ReplayMoveTrack> MoveTracks,
    IReadOnlyList<ReplayCastEntry> Casts,
    IReadOnlyList<ReplayFocusEntry> Focuses);

