using System;
using System.Collections.Generic;
using BattlePhaseEnum = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Entities.SyncData;
using LiteEntitySystem;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Entities;

/// <summary>
/// 战斗房间的网络同步 Entity。房间级战斗状态权威载体。
/// 创建单位与开始战斗的请求已由大厅 SignalR 通道（AddPrepareUnit / StartBattle）承担，
/// 本实体承载房间级战斗状态，战斗世界 BattleScene 经 IBattleRoom 直接读写。
/// 同步字段全部以服务端写回为准，禁止在 OnConstructed 重置：
/// LES 1.2.2 客户端先应用初始同步状态再执行 OnConstructed，重置会让一次性写入字段
/// （BattleStartUnixTime、BattlePhase 等）在客户端丢失且不再回补。
/// </summary>
public partial class BattleRoomEntity : EntityLogic {
    /// <summary>房间唯一 ID。</summary>
    public readonly SyncString RoomId = new();

    /// <summary>战斗阶段，对应 BattlePhase 枚举的 byte 值。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<ulong> BattlePhase;

    /// <summary>战斗是否已结束。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<bool> IsFinished;

    /// <summary>战斗开始时间，Unix 秒，UTC，服务端权威，战斗开始时写入。</summary>
    [SyncVarFlags(SyncFlags.NeverRollBack)]
    public SyncVar<long> BattleStartUnixTime;

    /// <summary>房间选中的副本键，服务端权威，客户端据此呈现对应环境场景。</summary>
    public readonly SyncString DungeonKey = new();

    /// <summary>战斗事件日志 RPC：整帧 SyncBattleEvent 数组广播。</summary>
    private static RemoteCallSpan<SyncBattleEvent> BattleEventsRPC;

    /// <summary>战斗事件日志接收事件，客户端解码后触发。参数：本帧领域事件。</summary>
    public event Action<IReadOnlyList<IBattleEvent>>? BattleEventsReceived;

    /// <summary>
    /// 初始化战斗房间实体。
    /// </summary>
    /// <param name="entityParams">实体框架参数。</param>
    public BattleRoomEntity(EntityParams entityParams) : base(entityParams) { }

    /// <summary>注册房间级 RPC：战斗事件日志整帧广播，服务端到全部客户端。</summary>
    protected override void RegisterRPC(ref RPCRegistrator r) {
        base.RegisterRPC(ref r);
        r.CreateRPCAction<BattleRoomEntity, SyncBattleEvent>(
            (e, events) => e.OnBattleEventsReceived(events),
            ref BattleEventsRPC,
            ExecuteFlags.SendToAll);
    }

    /// <summary>客户端接收：整帧事件日志解码为领域事件并触发 <see cref="BattleEventsReceived"/>。</summary>
    private void OnBattleEventsReceived(ReadOnlySpan<SyncBattleEvent> events) {
        var decoded = new List<IBattleEvent>(events.Length);
        foreach (var e in events) {
            if (BattleEventCoder.Decode(e) is { } domainEvent)
                decoded.Add(domainEvent);
        }
        BattleEventsReceived?.Invoke(decoded);
    }

    /// <summary>服务端调用：整帧事件日志编码广播，空帧不发。仅房间线程调用。</summary>
    public void BroadcastBattleEvents(ReadOnlySpan<SyncBattleEvent> events) {
        if (events.Length == 0)
            return;
        ExecuteRPC(BattleEventsRPC, events);
    }

    /// <summary>战斗开始：写入 Running 阶段、未结束与开始时刻。服务端权威，客户端不调用。</summary>
    public void ProjectBattleStarted() {
        if (!IsServer)
            return;
        BattlePhase.Value = (byte)BattlePhaseEnum.Running;
        IsFinished.Value = false;
        BattleStartUnixTime.Value = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    /// <summary>战斗结束：写入 Finished 阶段与已结束。服务端权威，客户端不调用。</summary>
    public void ProjectBattleEnded() {
        if (!IsServer)
            return;
        BattlePhase.Value = (byte)BattlePhaseEnum.Finished;
        IsFinished.Value = true;
    }
}

