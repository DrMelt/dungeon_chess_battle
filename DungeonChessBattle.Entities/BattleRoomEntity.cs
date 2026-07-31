using LiteEntitySystem;
using LiteEntitySystem.Extensions;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。纯数据载体。
/// </summary>
public class BattleRoomEntity : EntityLogic {
    public readonly SyncString RoomId = new();
    public SyncVar<byte> BattlePhase;
    public SyncVar<bool> IsFinished;
    public SyncVar<byte> WinnerCamp;

    // ── RPC ──────────────────────────────────────────────
    private static RemoteCallSerializable<SyncCreateUnitRequest> CreateUnitRPC;
    private static RemoteCall StartBattleRPC;

    // ── 实例事件（每个房间独立订阅） ────────────────────
    /// <summary>客户端请求创建单位。参数：房间实体、创建请求数据</summary>
    public event Action<BattleRoomEntity, SyncCreateUnitRequest>? CreateUnitRequested;

    /// <summary>客户端请求开始战斗。参数：房间实体</summary>
    public event Action<BattleRoomEntity>? StartBattleRequested;

    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }

    protected override void OnConstructed() {
        BattlePhase.Value = 0;
        IsFinished.Value = false;
        WinnerCamp.Value = 0;
    }

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
