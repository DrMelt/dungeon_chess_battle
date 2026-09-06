using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Replay;

namespace DungeonChessBattle.Game.BattleScene;

/// <summary>
/// 战斗表现层统一读契约：单位视图、本地玩家语义、权威元信息、事件日志与帧事件
/// 一律由当前装配源提供。在线装配为 <see cref="OnlineBattleViewSource"/>，
/// 回放装配为 <see cref="ReplayBattleViewSource"/>，两者由各自协调器单独构建注入。
/// </summary>
public interface IBattleViewSource {
    /// <summary>全部展示单位视图。</summary>
    IReadOnlyList<IUnitUiView> Units {
        get;
    }

    /// <summary>按单位 ID 查展示单位，不存在返回 null。</summary>
    IUnitUiView? FindUnit(UnitId unitId);

    /// <summary>本地玩家单位的展示视图；无本地控制器语义时为 null。</summary>
    IUnitUiView? LocalUnit {
        get;
    }

    /// <summary>本地玩家聚焦目标的展示视图；无聚焦目标或无本地控制器时为 null。</summary>
    IUnitUiView? LocalFocus {
        get;
    }

    /// <summary>当前副本键。</summary>
    string? DungeonKey {
        get;
    }

    /// <summary>战斗开始时刻，UTC Unix 秒。</summary>
    long? BattleStartUnixTime {
        get;
    }

    /// <summary>战斗已运行秒数；未就绪时为 null。</summary>
    double? BattleElapsed {
        get;
    }

    /// <summary>会话事件日志的只读视图。</summary>
    IReadOnlyList<BattleEventLogEntry> EventLog {
        get;
    }

    /// <summary>事件日志会话版本，装配重建时归零重计，消费方据此归零游标重同步。</summary>
    long EventLogVersion {
        get;
    }

    /// <summary>本装配是否在场（装配对象存在即在战斗/回放中）。</summary>
    bool IsInBattle {
        get;
    }

    /// <summary>驱动方投喂一帧领域事件：入帧缓冲，日志落账时机由装配决定。</summary>
    void AppendEvents(IReadOnlyList<IBattleEvent> events);

    /// <summary>取走并清空帧事件缓冲，表现组件每帧消费一次。</summary>
    IReadOnlyList<IBattleEvent> DrainFrameEvents();

    /// <summary>获取阵营关系函数用于领域判定；未就绪返回 false。</summary>
    bool TryGetCampRelations([NotNullWhen(true)] out CampRelationResolver? relations);

    /// <summary>解析目标阵营列表相对本地玩家的关系；本地单位或关系函数未就绪返回 Unknown。</summary>
    CampRelation ResolveLocalCampRelation(IReadOnlyList<string> targetCamps);
}

/// <summary>
/// 装配基类：帧事件缓冲与阵营关系按权威副本键懒装配两路共用；
/// 日志落账时机由子类 <see cref="AppendEventLog"/> 决定。
/// </summary>
public abstract class BattleViewSourceBase : IBattleViewSource {
    /// <summary>帧事件缓冲，AppendEvents 追加、DrainFrameEvents 取走。</summary>
    private List<IBattleEvent> _frameEvents = [];

    /// <summary>阵营关系函数缓存。</summary>
    private CampRelationResolver? _relations;

    /// <inheritdoc />
    public abstract IReadOnlyList<IUnitUiView> Units {
        get;
    }

    /// <inheritdoc />
    public abstract IUnitUiView? FindUnit(UnitId unitId);

    /// <inheritdoc />
    public abstract IUnitUiView? LocalUnit {
        get;
    }

    /// <inheritdoc />
    public abstract IUnitUiView? LocalFocus {
        get;
    }

    /// <inheritdoc />
    public abstract string? DungeonKey {
        get;
    }

    /// <inheritdoc />
    public abstract long? BattleStartUnixTime {
        get;
    }

    /// <inheritdoc />
    public abstract double? BattleElapsed {
        get;
    }

    /// <inheritdoc />
    public abstract IReadOnlyList<BattleEventLogEntry> EventLog {
        get;
    }

    /// <inheritdoc />
    public abstract long EventLogVersion {
        get;
    }

    /// <inheritdoc />
    public bool IsInBattle => true;

    /// <summary>本帧事件的日志落账：在线已在会话侧入库为空操作，回放按引擎帧轴入本地仓库。</summary>
    protected abstract void AppendEventLog(IReadOnlyList<IBattleEvent> events);

    /// <inheritdoc />
    public void AppendEvents(IReadOnlyList<IBattleEvent> events) {
        if (events.Count == 0)
            return;
        _frameEvents.AddRange(events);
        AppendEventLog(events);
    }

    /// <inheritdoc />
    public IReadOnlyList<IBattleEvent> DrainFrameEvents() {
        if (_frameEvents.Count == 0)
            return [];
        var frame = _frameEvents;
        _frameEvents = [];
        return frame;
    }

    /// <inheritdoc />
    public bool TryGetCampRelations([NotNullWhen(true)] out CampRelationResolver? relations) {
        var resolved = RelationsOrResolve();
        relations = resolved;
        return resolved != null;
    }

    /// <inheritdoc />
    public CampRelation ResolveLocalCampRelation(IReadOnlyList<string> targetCamps) {
        var relations = RelationsOrResolve();
        var localUnit = LocalUnit;
        if (relations == null || localUnit == null)
            return CampRelation.Unknown;
        return relations.Invoke(localUnit.Camps, targetCamps);
    }

    /// <summary>阵营关系读取：未装配且权威副本键已到达时即收敛装配，未知键抛异常不静默回退。</summary>
    private CampRelationResolver? RelationsOrResolve() {
        if (_relations is { } relations)
            return relations;
        var dungeonKey = DungeonKey;
        if (string.IsNullOrWhiteSpace(dungeonKey))
            return null;
        return _relations = DungeonRegistry.Instance.GetRelations(dungeonKey);
    }
}

/// <summary>
/// 在线装配：取数、事件日志与本地语义全部委托权威会话；
/// 运行时长由本地时钟相对权威开始时刻推算，事件已在会话侧带接收时刻入库，不重复落账。
/// </summary>
public sealed class OnlineBattleViewSource(IClientBattleSession session) : BattleViewSourceBase {
    /// <inheritdoc />
    public override IReadOnlyList<IUnitUiView> Units => session.Units;

    /// <inheritdoc />
    public override IUnitUiView? FindUnit(UnitId unitId) => session.FindUnit(unitId);

    /// <inheritdoc />
    public override IUnitUiView? LocalUnit => session.LocalUnit;

    /// <inheritdoc />
    public override IUnitUiView? LocalFocus => session.LocalFocus;

    /// <inheritdoc />
    public override string? DungeonKey => session.DungeonKey;

    /// <inheritdoc />
    public override long? BattleStartUnixTime => session.BattleStartUnixTime;

    /// <inheritdoc />
    public override double? BattleElapsed {
        get {
            if (session.BattleStartUnixTime is not { } start || start <= 0)
                return null;
            return (DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(start)).TotalSeconds;
        }
    }

    /// <inheritdoc />
    public override IReadOnlyList<BattleEventLogEntry> EventLog => session.GetEventLog();

    /// <inheritdoc />
    public override long EventLogVersion => session.GetEventLogVersion();

    /// <inheritdoc />
    protected override void AppendEventLog(IReadOnlyList<IBattleEvent> events) {
    }
}

/// <summary>
/// 回放装配：取数经回放引擎世界读数；无本地控制器，本地玩家语义恒 null；
/// 事件按引擎帧轴折算接收时刻落本装配自持的日志仓库，与在线同数轴。
/// </summary>
public sealed class ReplayBattleViewSource(ReplayEngine engine) : BattleViewSourceBase {
    /// <summary>回放事件日志仓库，随本装配生死。</summary>
    private readonly BattleEventLogStore _eventLog = new();

    /// <inheritdoc />
    public override IReadOnlyList<IUnitUiView> Units => engine.Units;

    /// <inheritdoc />
    public override IUnitUiView? FindUnit(UnitId unitId) => engine.FindUnit(unitId);

    /// <inheritdoc />
    public override IUnitUiView? LocalUnit => null;

    /// <inheritdoc />
    public override IUnitUiView? LocalFocus => null;

    /// <inheritdoc />
    public override string? DungeonKey => engine.DungeonKey;

    /// <inheritdoc />
    public override long? BattleStartUnixTime => engine.BattleStartUnixTime;

    /// <inheritdoc />
    public override double? BattleElapsed => engine.ElapsedSeconds;

    /// <inheritdoc />
    public override IReadOnlyList<BattleEventLogEntry> EventLog => _eventLog.Entries;

    /// <inheritdoc />
    public override long EventLogVersion => _eventLog.Version;

    /// <inheritdoc />
    protected override void AppendEventLog(IReadOnlyList<IBattleEvent> events) =>
        _eventLog.Append(events, engine.FrameUnixMs);
}

