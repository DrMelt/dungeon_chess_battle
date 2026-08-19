using System.Collections.Generic;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 战斗会话上下文：当前房间战斗数据与玩家操作门面。
/// 持有战斗服务、房间 ID、副本阵营关系与战斗开始时刻；聚焦目标提交与循环切换、
/// 施法通道与阵营判定均由此组件的只读能力提供。由 MainScene 进出战斗时 Bind/Unbind。
/// 单位视图（UnitGameShow）生命周期归 UnitShowManager，本组件不创建任何视图。
/// </summary>
public partial class BattleSessionContext : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleSessionContext> _logger = ServiceLocator.GetLogger<BattleSessionContext>();

    /// <summary>当前战斗服务（Bind 时注入，用于施法与聚焦 RPC）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>当前战斗服务引用（供技能链发起施法 RPC）。</summary>
    public IClientBattleService? BattleService => _battleService;

    /// <summary>房间客户端（Bind 时注入，用于 Pawn 查询与权威数据投影）。</summary>
    private RoomBattleClient? _roomClient;

    /// <summary>当前房间 ID（Bind 时注入）。</summary>
    private string _roomId = "";

    /// <summary>当前房间 ID（供技能链发起施法 RPC）。</summary>
    public string RoomId => _roomId;

    /// <summary>当前房间副本键，来自服务端权威 BattleRoomEntity 同步；实体未同步时为 null。</summary>
    public string? DungeonKey => _roomClient?.DungeonKey;

    /// <summary>战斗开始时刻（服务端权威 Unix 秒），未进战斗或实体未同步时为 null。</summary>
    public long? BattleStartUnixTime => _roomClient?.BattleStartUnixTime;

    /// <summary>本地玩家控制的单位 Pawn，控制器未就绪时返回 null。</summary>
    public UnitPawn? LocalUnitPawn => _roomClient?.LocalUnitPawn;

    /// <summary>场景全部单位 Pawn 快照，直接派生自客户端网络实体集合（UI 展示数据源）。</summary>
    public IReadOnlyList<UnitPawn> Units => _roomClient?.GetPawns() ?? [];

    /// <summary>本地玩家单位的聚焦目标 Pawn；焦点为 0 或实体未到达时返回 null。</summary>
    public UnitPawn? LocalFocusPawn {
        get {
            var pawn = LocalUnitPawn;
            if (pawn == null)
                return null;
            ushort targetNetId = pawn.FocusTargetNetId.Value;
            return targetNetId != 0 ? _roomClient?.FindPawnById(targetNetId) : null;
        }
    }

    /// <summary>阵营关系函数，Bind 后按权威副本键装配并随房间实体同步收敛；null 表示未就绪。</summary>
    private CampRelationResolver? _relations;

    /// <summary>循环切换目标的客户端游标，乐观推进容忍聚焦回包延迟，手动选择时对齐。</summary>
    private ushort _cycleTargetId;

    /// <summary>
    /// 进入战斗：注入服务与房间客户端，装配阵营关系函数。
    /// </summary>
    public void Bind(IClientBattleService service, RoomBattleClient roomClient, string roomId) {
        _battleService = service;
        _roomClient = roomClient;
        _roomId = roomId;
        _relations = TryAssembleRelations(roomClient.DungeonKey);
    }

    /// <summary>退出战斗：释放全部会话引用。</summary>
    public void Unbind() {
        _battleService = null;
        _roomClient = null;
        _roomId = "";
        _relations = null;
        _cycleTargetId = 0;
    }

    /// <summary>节点退出场景树：兜底释放（防止战斗中途场景被释放导致引用悬挂）。</summary>
    public override void _ExitTree() {
        Unbind();
    }

    /// <summary>
    /// 请求本地玩家单位设置聚焦目标，0 表示清除。
    /// 经 IClientBattleService 发送 RPC，服务端校验后写回权威状态。
    /// </summary>
    /// <param name="targetNetId">目标单位网络 ID，0 表示清除。</param>
    public void SetLocalFocusTarget(ushort targetNetId) {
        var pawn = _roomClient?.LocalUnitPawn;
        if (pawn == null || _battleService == null) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(
                    "Focus RPC dropped: hasPawn={HasPawn}, hasService={HasService}, target={TargetId}",
                    pawn != null, _battleService != null, targetNetId);
            return;
        }
        _battleService.SetFocusTarget(_roomId, pawn.Id, targetNetId);
        _cycleTargetId = targetNetId;
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("SetLocalFocusTarget: selector={UnitId} -> target={TargetId}",
                pawn.Id, targetNetId);
    }

    /// <summary>
    /// 切换本地聚焦目标为下一个存活敌方单位。
    /// 从当前循环游标之后的单位开始轮询；游标失效时回退服务端权威焦点，
    /// 仍不可用时从第一个敌方单位开始；没有存活敌方单位时不发起请求。
    /// </summary>
    public void CycleEnemyTarget() {
        var pawn = _roomClient?.LocalUnitPawn;
        if (pawn == null || _battleService == null) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning(
                    "CycleEnemyTarget dropped: hasPawn={HasPawn}, hasService={HasService}",
                    pawn != null, _battleService != null);
            return;
        }

        var enemies = GetLivingEnemies(pawn);
        if (enemies.Count == 0) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("CycleEnemyTarget: no living enemy targets.");
            return;
        }

        int index = enemies.FindIndex(e => e.Id == _cycleTargetId);
        if (index < 0)
            index = enemies.FindIndex(e => e.Id == pawn.FocusTargetNetId.Value);
        ushort next = enemies[(index + 1) % enemies.Count].Id;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("CycleEnemyTarget: cycle={CycleId}, focus={FocusId} -> next={NextId}",
                _cycleTargetId, pawn.FocusTargetNetId.Value, next);
        SetLocalFocusTarget(next);
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
        if (relations == null && _roomClient != null) {
            string? dungeonKey = _roomClient.DungeonKey;
            if (!string.IsNullOrWhiteSpace(dungeonKey))
                relations = _relations = DungeonRegistry.Instance.GetRelations(dungeonKey);
        }
        return relations;
    }

    /// <summary>
    /// 获取阵营关系函数用于领域判定（技能预拦等）；未就绪返回 false。
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
        var localPawn = _roomClient?.LocalUnitPawn;
        if (relations == null || localPawn == null)
            return CampRelation.Unknown;
        return relations.Invoke(localPawn.CampTags, targetCamps);
    }

    /// <summary>Running 阶段就绪校验：战斗已开始仍无阵营判定能力属时序故障，响亮报告。</summary>
    public void AssertCampRelationsReady() {
        if (_relations == null) {
            _logger.LogError(
                "[BattleSessionContext] 战斗 Running 阶段阵营关系仍未装配（DungeonKey 未同步），技能预拦将被拒绝。");
        }
    }

    /// <summary>
    /// 战斗阶段 Running 的会话侧响应：就绪校验等会话业务在此收敛，
    /// MainScene 只下发阶段通知，不承载具体会话细节。
    /// </summary>
    public void OnBattleRunning() {
        AssertCampRelationsReady();
    }

    /// <summary>收集与本地单位阵营敌对且存活（UnitState==0）的单位，按房间 Pawn 顺序排列。</summary>
    private List<UnitPawn> GetLivingEnemies(UnitPawn self) {
        List<UnitPawn> enemies = [];
        var pawns = _roomClient?.GetPawns();
        if (pawns == null)
            return enemies;
        foreach (var candidate in pawns) {
            if (candidate.Id == self.Id || candidate.UnitState.Value == 1)
                continue;
            if (ResolveLocalCampRelation(candidate.CampTags) != CampRelation.Enemy)
                continue;
            enemies.Add(candidate);
        }
        return enemies;
    }
}
