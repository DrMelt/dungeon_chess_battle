using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。纯数据载体。
/// 创建单位与开始战斗的请求已由大厅 SignalR 通道（AddPrepareUnit / StartBattle）承担，
/// 本实体只同步房间级战斗状态。
/// </summary>
public class BattleRoomEntity : EntityLogic {
    /// <summary>房间唯一 ID。</summary>
    public readonly SyncString RoomId = new();

    /// <summary>战斗阶段，对应 BattlePhase 枚举的 byte 值。</summary>
    public SyncVar<ulong> BattlePhase;

    /// <summary>战斗是否已结束。</summary>
    public SyncVar<bool> IsFinished;

    /// <summary>胜方阵营字符串标识，如 "Camp_A"、"Camp_B"，空表示未知或无胜方。</summary>
    public readonly SyncString WinnerCamp = new();

    /// <summary>房间创建时间，Unix 秒，UTC，服务端权威。</summary>
    public SyncVar<double> CreatedUnixTime;

    /// <summary>房间选中的副本键，服务端权威，客户端据此呈现对应环境场景。</summary>
    public readonly SyncString DungeonKey = new();

    /// <summary>
    /// 初始化战斗房间实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// 实体构造完成回调：初始化默认战斗状态。
    /// ⚠ LiteEntitySystem 1.2.2 语义：OnConstructed 在 AddEntity(initAction) 之后执行，
    /// 会覆盖服务端注入值。此处仅保留纯内部默认状态；
    /// 运行时注入字段，RoomId、WinnerCamp 等，禁止在此赋默认值。
    /// </summary>
    protected override void OnConstructed() {
        BattlePhase.Value = 0;
        IsFinished.Value = false;
        WinnerCamp.Value = string.Empty;
        CreatedUnixTime.Value = 0;
    }
}
