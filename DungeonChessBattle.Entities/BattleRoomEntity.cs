using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。纯数据载体。
/// </summary>
public class BattleRoomEntity : EntityLogic {
    /// <summary>房间唯一 ID。</summary>
    public readonly SyncString RoomId = new();

    /// <summary>战斗阶段（对应 BattlePhase 枚举的 byte 值）。</summary>
    public SyncVar<ulong> BattlePhase;

    /// <summary>战斗是否已结束。</summary>
    public SyncVar<bool> IsFinished;

    /// <summary>胜方阵营字符串标识（如 "Camp_A"、"Camp_B"，空=未知/无胜方）。</summary>
    public readonly SyncString WinnerCamp = new();

    /// <summary>房间创建时间（Unix 秒，UTC，服务端权威）。</summary>
    public SyncVar<double> CreatedUnixTime;

    private static RemoteCallSerializable<SyncCreateUnitRequest> CreateUnitRPC;
    private static RemoteCall StartBattleRPC;

    /// <summary>客户端请求创建单位。参数：房间实体、创建请求数据</summary>
    public event Action<BattleRoomEntity, SyncCreateUnitRequest>? CreateUnitRequested;

    /// <summary>客户端请求开始战斗。参数：房间实体</summary>
    public event Action<BattleRoomEntity>? StartBattleRequested;

    /// <summary>
    /// 初始化战斗房间实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }

    /// <summary>
    /// 实体构造完成回调：初始化默认战斗状态。
    /// ⚠ LiteEntitySystem 1.2.2 语义：OnConstructed 在 AddEntity(initAction) 之后执行，
    /// 会覆盖服务端注入值。此处仅保留纯内部默认状态；
    /// 运行时注入字段（RoomId/WinnerCamp 等）禁止在此赋默认值。
    /// </summary>
    protected override void OnConstructed() {
        BattlePhase.Value = 0;
        IsFinished.Value = false;
        WinnerCamp.Value = string.Empty;
        CreatedUnixTime.Value = 0;
    }

    /// <summary>
    /// 注册 RPC 动作：创建单位请求与开始战斗请求（均在服务端执行）。
    /// </summary>
    /// <param name="r">RPC 注册器。</param>
    protected override void RegisterRPC(ref RPCRegistrator r) {
        base.RegisterRPC(ref r);
        r.CreateRPCAction<BattleRoomEntity, SyncCreateUnitRequest>(
            (e, req) => CreateUnitRequested?.Invoke(e, req),
            ref CreateUnitRPC,
            ExecuteFlags.ExecuteOnServer);
        r.CreateRPCAction<BattleRoomEntity>(
            e => StartBattleRequested?.Invoke(e),
            ref StartBattleRPC,
            ExecuteFlags.ExecuteOnServer);
    }

    /// <summary>客户端调用：请求创建单位</summary>
    public void RequestCreateUnit(SyncCreateUnitRequest req) {
        ExecuteRPC(CreateUnitRPC, req);
    }

    /// <summary>客户端调用：请求开始战斗</summary>
    public void RequestStartBattle() {
        ExecuteRPC(StartBattleRPC);
    }
}
