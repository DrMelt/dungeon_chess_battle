using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。纯数据载体。
/// 创建单位与开始战斗的请求已由大厅 SignalR 通道（AddPrepareUnit / StartBattle）承担，
/// 本实体只同步房间级战斗状态。
/// 同步字段全部以服务端写回为准，禁止在 OnConstructed 重置：
/// LES 1.2.2 客户端先应用初始同步状态再执行 OnConstructed，重置会让一次性写入字段
/// （BattleStartUnixTime、BattlePhase 等）在客户端丢失且不再回补。
/// </summary>
public class BattleRoomEntity : EntityLogic {
    /// <summary>房间唯一 ID。</summary>
    public readonly SyncString RoomId = new();

    /// <summary>战斗阶段，对应 BattlePhase 枚举的 byte 值。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<ulong> BattlePhase;

    /// <summary>战斗是否已结束。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<bool> IsFinished;

    /// <summary>胜方阵营字符串标识，如 "Camp_A"、"Camp_B"，空表示未知或无胜方。</summary>
    public readonly SyncString WinnerCamp = new();

    /// <summary>战斗开始时间，Unix 秒，UTC，服务端权威，BattleStarted 事件时写入。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<long> BattleStartUnixTime;

    /// <summary>房间选中的副本键，服务端权威，客户端据此呈现对应环境场景。</summary>
    public readonly SyncString DungeonKey = new();

    /// <summary>
    /// 初始化战斗房间实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }
}
