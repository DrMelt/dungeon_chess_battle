using System.Collections.Generic;
using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.BattleScene;

/// <summary>
/// 战斗会话上下文（门面）：表现层唯一的战斗数据入口，全部读数转发当前装配的
/// <see cref="IBattleViewSource"/>；未绑定态取数恒空，消费方无需判空。
/// 命令写侧转发 <see cref="IBattleSessionCommand"/>，仅在线装配存在，回放与未绑定态为 null。
/// 装配对象由 BattleCoordinator / ReplayCoordinator 单独构建注入，本节点不认识在线会话与回放引擎类型。
/// 本节点随 battle_world 场景在场，进出战斗与回放只由编排器 Bind/Unbind，
/// 其余表现组件直持本节点引用取数，单位视图生命周期归 UnitShowManager。
/// </summary>
public partial class BattleSessionContext : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleSessionContext> _logger = ServiceLocator.GetLogger<BattleSessionContext>();

    /// <summary>当前读侧装配，未绑定为 null。</summary>
    private IBattleViewSource? _source;

    /// <summary>当前命令装配，仅在线绑定非空。</summary>
    private IBattleSessionCommand? _command;

    /// <summary>绑定代次，每次 Bind/Unbind 递增。表现组件收不到进出通知，据此自检数据源换向。</summary>
    public long BindGeneration {
        get; private set;
    }

    /// <summary>玩家命令窄契约（供技能面板直接消费，UI 不接触服务）；回放与未绑定态为 null。</summary>
    public IBattleSessionCommand? Command => _command;

    // =============================================================
    // 只读投影（转发当前装配，未绑定恒空）
    // =============================================================

    /// <summary>全部展示单位视图，未绑定恒空。</summary>
    public IReadOnlyList<IUnitUiView> Units => _source?.Units ?? [];

    /// <summary>按单位 ID 查展示单位，不存在返回 null。</summary>
    public IUnitUiView? FindUnit(UnitId unitId) => _source?.FindUnit(unitId);

    /// <summary>本地玩家单位的展示视图；回放或未就绪时为 null。</summary>
    public IUnitUiView? LocalUnit => _source?.LocalUnit;

    /// <summary>本地玩家聚焦目标的展示视图；无聚焦目标或无本地控制器时为 null。</summary>
    public IUnitUiView? LocalFocus => _source?.LocalFocus;

    /// <summary>当前副本键；未就绪时为 null。</summary>
    public string? DungeonKey => _source?.DungeonKey;

    /// <summary>战斗开始时刻，UTC Unix 秒；未就绪时为 null。</summary>
    public long? BattleStartUnixTime => _source?.BattleStartUnixTime;

    /// <summary>战斗已运行秒数；未就绪时为 null。</summary>
    public double? BattleElapsed => _source?.BattleElapsed;

    /// <summary>是否已绑定战斗或回放。</summary>
    public bool IsInBattle => _source?.IsInBattle ?? false;

    /// <summary>会话事件日志的只读视图，未绑定恒空。</summary>
    public IReadOnlyList<BattleEventLogEntry> EventLog => _source?.EventLog ?? [];

    /// <summary>事件日志会话版本，装配重建时归零重计，消费方据此归零游标重同步。</summary>
    public long EventLogVersion => _source?.EventLogVersion ?? 0;

    /// <summary>获取阵营关系函数用于领域判定；未就绪返回 false。</summary>
    public bool TryGetCampRelations(out CampRelationResolver relations) {
        if (_source is { } source)
            return source.TryGetCampRelations(out relations);
        relations = null!;
        return false;
    }

    /// <summary>解析目标阵营列表相对本地玩家的关系；本地单位或关系函数未就绪返回 Unknown。</summary>
    public CampRelation ResolveLocalCampRelation(IReadOnlyList<string> targetCamps)
        => _source?.ResolveLocalCampRelation(targetCamps) ?? CampRelation.Unknown;

    /// <summary>驱动方投喂一帧领域事件：交装配入帧缓冲，日志落账由装配自决。</summary>
    public void AppendEvents(IReadOnlyList<IBattleEvent> events) => _source?.AppendEvents(events);

    /// <summary>取走并清空帧事件缓冲，表现组件每帧消费一次。</summary>
    public IReadOnlyList<IBattleEvent> DrainFrameEvents() => _source?.DrainFrameEvents() ?? [];

    // =============================================================
    // 玩家命令（转发在线装配，回放与未绑定态无装配不受理）
    // =============================================================

    /// <summary>请求本地玩家单位设置聚焦目标，0 表示清除。</summary>
    public void SetLocalFocusTarget(ushort targetNetId) => _command?.SetLocalFocusTarget(targetNetId);

    // =============================================================
    // 装配生命周期
    // =============================================================

    /// <summary>
    /// 装配注入：设置当前读侧装配与可选命令装配，递增绑定代次令常驻组件自检换向。
    /// 装配对象由调用方（在线/回放编排器）单独构建，换绑即旧装配整体废弃。
    /// </summary>
    public void Bind(IBattleViewSource source, IBattleSessionCommand? command = null) {
        _source = source;
        _command = command;
        BindGeneration++;
    }

    /// <summary>退出战斗或回放：释放全部装配引用，回到未绑定恒空态。</summary>
    public void Unbind() {
        _source = null;
        _command = null;
        BindGeneration++;
    }

    /// <summary>节点退出场景树：兜底释放（防止战斗中途场景被释放导致引用悬挂）。</summary>
    public override void _ExitTree() {
        Unbind();
    }

    /// <summary>战斗阶段 Running 的会话侧响应：战斗已开始仍无阵营判定能力属时序故障，响亮报告。</summary>
    public void OnBattleRunning() {
        if (_source != null && !_source.TryGetCampRelations(out _)) {
            _logger.LogError(
                "[BattleSessionContext] 战斗 Running 阶段阵营关系仍未装配（DungeonKey 未同步），技能目标选择与聚焦切换将被拒绝。");
        }
    }
}
