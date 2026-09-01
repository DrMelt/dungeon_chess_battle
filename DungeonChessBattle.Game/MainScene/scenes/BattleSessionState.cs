using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Client;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.GameConfig;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.MainScene.scenes;

/// <summary>
/// 战斗会话只读投影：把当前房间的权威实体与领域状态投影为 UI 可消费的只读视图。
/// 实现 <see cref="IBattleViewSource"/> 统一在线投影口径，另提供本地/聚焦/施法语义视图、
/// 事件日志流读取与阵营关系判定能力。
/// 不承载玩家命令（写侧归 <see cref="IBattleSessionCommand"/>）与交互循环状态。
/// </summary>
public sealed class BattleSessionState : IBattleViewSource {
    /// <summary>日志记录器。</summary>
    private readonly ILogger<BattleSessionState> _logger = ServiceLocator.GetLogger<BattleSessionState>();

    /// <summary>在线战斗会话（Bind 时注入），提供展示取数、本地语义与权威元信息。</summary>
    private IClientBattleSession? _session;

    /// <summary>阵营关系函数，按权威副本键装配并随房间实体同步收敛；null 表示未就绪。</summary>
    private CampRelationResolver? _relations;

    /// <summary>是否已绑定会话（Bind 后 true）。</summary>
    public bool IsInBattle => _session != null;

    /// <summary>进入战斗：注入在线战斗会话并尝试装配阵营关系函数。</summary>
    public void Bind(IClientBattleSession session) {
        _session = session;
        _relations = TryAssembleRelations(session.DungeonKey);
    }

    /// <summary>退出战斗：释放会话引用与缓存的阵营关系。</summary>
    public void Unbind() {
        _session = null;
        _relations = null;
    }

    /// <inheritdoc />
    public IReadOnlyList<IUnitUiView> Units => _session?.Units ?? [];

    /// <inheritdoc />
    public IUnitUiView? FindUnit(UnitId unitId) => _session?.FindUnit(unitId);

    /// <summary>本地玩家的展示视图，控制器未就绪时返回 null。</summary>
    public IUnitUiView? LocalUnit => _session?.LocalUnit;

    /// <summary>本地玩家的聚焦目标展示视图；焦点为 0 或无目标时返回 null。</summary>
    public IUnitUiView? LocalFocus => _session?.LocalFocus;

    /// <summary>当前房间副本键，来自服务端权威房间实体同步；实体未同步时为 null。</summary>
    public string? DungeonKey => _session?.DungeonKey;

    /// <summary>战斗开始时刻（服务端权威 Unix 秒），未进战斗或实体未同步时为 null。</summary>
    public long? BattleStartUnixTime => _session?.BattleStartUnixTime;

    /// <summary>当前房间会话事件日志的只读视图（规避 UI 直取服务）。</summary>
    public IReadOnlyList<BattleEventLogEntry> EventLog => _session?.GetEventLog() ?? [];

    /// <summary>当前房间会话事件日志版本号，会话重置时自增。</summary>
    public long EventLogVersion => _session?.GetEventLogVersion() ?? 0;

    /// <summary>
    /// 获取阵营关系函数用于领域判定（技能目标选择、聚焦切换等）；未就绪返回 false。
    /// 调用方取得函数后必须转交领域校验器，不得自行判敌我。
    /// </summary>
    public bool TryGetCampRelations(out CampRelationResolver relations) {
        var resolved = RelationsOrResolve();
        relations = resolved ?? null!;
        return resolved != null;
    }

    /// <summary>解析目标阵营列表相对本地玩家的关系；本地单位或关系函数未就绪返回 Unknown。</summary>
    public CampRelation ResolveLocalCampRelation(IReadOnlyList<string> targetCamps) {
        var relations = RelationsOrResolve();
        var localUnit = _session?.LocalUnit;
        if (relations == null || localUnit == null)
            return CampRelation.Unknown;
        return relations.Invoke(localUnit.Camps, targetCamps);
    }

    /// <summary>Running 阶段就绪校验：战斗已开始仍无阵营判定能力属时序故障，响亮报告。</summary>
    public void AssertCampRelationsReady() {
        if (_relations == null) {
            _logger.LogError(
                "[BattleSessionState] 战斗 Running 阶段阵营关系仍未装配（DungeonKey 未同步），技能目标选择与聚焦切换将被拒绝。");
        }
    }

    /// <summary>
    /// 尝试按权威副本键装配阵营关系函数；键未同步时返回 null，由调用方按未就绪处理。
    /// 未知键抛异常（配置故障响亮暴露），绝不静默回退到其他语义。
    /// </summary>
    private static CampRelationResolver? TryAssembleRelations(string? dungeonKey)
        => string.IsNullOrWhiteSpace(dungeonKey) ? null : DungeonRegistry.Instance.GetRelations(dungeonKey);

    /// <summary>
    /// 阵营关系函数读取：未就绪且权威副本键已同步时即收敛装配。
    /// 房间实体同步到达后，任意一次判定访问都会自动取得实战关系，无需轮询。
    /// </summary>
    private CampRelationResolver? RelationsOrResolve() {
        var relations = _relations;
        if (relations == null && _session != null) {
            string? dungeonKey = _session.DungeonKey;
            if (!string.IsNullOrWhiteSpace(dungeonKey))
                relations = _relations = DungeonRegistry.Instance.GetRelations(dungeonKey);
        }
        return relations;
    }
}
