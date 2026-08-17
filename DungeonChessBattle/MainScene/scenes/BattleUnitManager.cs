using System.Collections.Generic;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GameAssets;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GamePanels;
using DungeonChessBattle.Services;
using BuffView = DungeonChessBattle.Battle.Domain.Combat.BuffView;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 战斗单位管理器：单位视图（UnitGameShow）的全生命周期所有者与服务端事件桥。
/// 仅本组件订阅网络/服务事件；本地玩家单位与聚焦目标（服务端权威
/// UnitPawn.FocusTargetNetId）投影为只读派生属性供 UI 每帧直读。
/// 由 MainScene 在进入/退出战斗时 Bind/Unbind。
/// </summary>
public partial class BattleUnitManager : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleUnitManager> _logger = ServiceLocator.GetLogger<BattleUnitManager>();

    /// <summary>单位展示场景（unit_game_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    /// <summary>场景单位集合资源（UI 层数据源：状态条、技能目标、计时等）。</summary>
    public UnitsInScene UnitsInSceneRes { get; } = new();

    /// <summary>战斗开始时刻（服务端权威 Unix 秒），未进战斗或实体未同步时为 null。</summary>
    public long? BattleStartUnixTime => _roomClient?.BattleStartUnixTime;

    /// <summary>场景单位 Pawn 数组快照。</summary>
    public List<UnitPawn> UnitsArr => UnitsInSceneRes.UnitsArr;

    /// <summary>当前战斗服务（Bind 时注入，用于事件订阅）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>当前战斗服务引用（供技能链发起施法 RPC）。</summary>
    public IClientBattleService? BattleService => _battleService;

    /// <summary>当前房间 ID（供技能链发起施法 RPC）。</summary>
    public string RoomId => _roomId;

    /// <summary>阵营关系函数，Bind 后按权威副本键装配并随房间实体同步收敛；null 表示未就绪。</summary>
    private CampRelationResolver? _relations;

    /// <summary>循环切换目标的客户端游标，乐观推进容忍聚焦回包延迟，手动选择时对齐。</summary>
    private ushort _cycleTargetId;

    /// <summary>本地玩家控制的单位视图，控制器或视图未就绪时返回 null。</summary>
    public UnitGameShow? LocalUnitShow {
        get {
            var pawn = _roomClient?.LocalUnitPawn;
            return pawn != null ? _unitShows.GetValueOrDefault(pawn.Id) : null;
        }
    }

    /// <summary>
    /// 本地玩家单位的聚焦目标视图。
    /// 直接从服务端权威 SyncVar UnitPawn.FocusTargetNetId 派生，视图未生成时为 null。
    /// </summary>
    public UnitGameShow? LocalFocusUnit {
        get {
            var pawn = _roomClient?.LocalUnitPawn;
            if (pawn == null)
                return null;
            ushort targetNetId = pawn.FocusTargetNetId.Value;
            return targetNetId != 0 ? _unitShows.GetValueOrDefault(targetNetId) : null;
        }
    }

    /// <summary>房间客户端（Bind 时注入，用于 Pawn 查询）。</summary>
    private RoomBattleClient? _roomClient;

    /// <summary>当前房间 ID（Bind 时注入）。</summary>
    private string _roomId = "";

    /// <summary>单位网络实体 ID → UnitGameShow 映射。</summary>
    private readonly Dictionary<ushort, UnitGameShow> _unitShows = [];

    /// <summary>
    /// 进入战斗：注入服务与房间客户端，订阅单位事件并初始化缓存单位。
    /// </summary>
    public void Bind(IClientBattleService service, RoomBattleClient roomClient, string roomId) {
        _battleService = service;
        _roomClient = roomClient;
        _roomId = roomId;
        _relations = TryAssembleRelations(roomClient.DungeonKey);

        service.OnUnitCreated += OnServiceUnitCreated;
        service.UnitHealthChanged += OnUnitHealth;
        service.UnitDied += OnUnitDied;
        service.UnitBuffAdded += OnBuffAdded;
        service.UnitBuffRemoved += OnBuffRemoved;

        InitializeUnitsFromPawns();
    }

    /// <summary>退出战斗：退订单位事件并清理全部单位视图。</summary>
    public void Unbind() {
        if (_battleService != null) {
            _battleService.OnUnitCreated -= OnServiceUnitCreated;
            _battleService.UnitHealthChanged -= OnUnitHealth;
            _battleService.UnitDied -= OnUnitDied;
            _battleService.UnitBuffAdded -= OnBuffAdded;
            _battleService.UnitBuffRemoved -= OnBuffRemoved;
        }

        ClearUnits();

        _battleService = null;
        _roomClient = null;
        _roomId = "";
        _relations = null;
        _cycleTargetId = 0;
    }

    /// <summary>节点退出场景树：兜底退订（防止战斗中途场景被释放导致事件悬挂）。</summary>
    public override void _ExitTree() {
        Unbind();
    }

    /// <summary>
    /// 服务事件：单位创建。网络模式下单位实体可能晚于战斗开始到达；
    /// 与 InitializeUnitsFromPawns 缓存兜底共用幂等入口，保证不重不漏。
    /// </summary>
    private void OnServiceUnitCreated(string eventRoomId, ushort netId, string unitName, string camp) {
        if (eventRoomId != _roomId)
            return;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Unit created via service: {UnitName} (camp={Camp}, netId={NetId})", unitName, camp, netId);
        CallDeferred(nameof(SpawnUnitFromCache), netId);
    }

    /// <summary>延迟生成单位（CallDeferred 入口）。</summary>
    private void SpawnUnitFromCache(ushort netId) {
        var pawn = _roomClient?.FindPawnById(netId);
        if (pawn is not null)
            TrySpawnUnit(pawn);
        else
            _logger.LogWarning("Unit netId={NetId} not found in pawn cache; entity may not have arrived yet", netId);
    }

    /// <summary>
    /// 幂等生成单位视图：同名单位已存在时跳过。
    /// 事件驱动路径（OnServiceUnitCreated）与缓存兜底路径（InitializeUnitsFromPawns）共用。
    /// </summary>
    private void TrySpawnUnit(UnitPawn pawn) {
        if (_unitShows.ContainsKey(pawn.Id))
            return;
        SpawnUnit(pawn);
    }

    private void SpawnUnit(UnitPawn pawn) {
        var unitName = pawn.UnitName.Value;

        // 按配置键取配置（技能资源构建来源）
        var config = UnitCatalog.GetByKey(unitName);

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        // 注入网络同步 Pawn（setter 先于挂载，_Ready 校验不会误报）
        unitShow.Pawn = pawn;

        // 从配置构建 Godot 技能资源列表，并向 Pawn 本地写入技能定义列表（不参与网络同步，两端各自从共享配置读取）
        if (config != null) {
            pawn.Skills = config.Skills;

            foreach (var skillDefinition in config.Skills) {
                unitShow.SkillsList.Add(SkillResourceTable.LoadResource(skillDefinition));
            }
        }

        UnitsInSceneRes.AddUnit(pawn);
        AddChild(unitShow);
        _unitShows[pawn.Id] = unitShow;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Spawned unit '{UnitName}' at {Position}", unitName, pawn.Position.Value);
    }

    private void InitializeUnitsFromPawns() {
        if (_roomClient == null)
            return;

        var pawns = _roomClient.GetPawns();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Initializing units: total={Total}", pawns.Count);

        foreach (var pawn in pawns)
            TrySpawnUnit(pawn);
    }

    private void ClearUnits() {
        foreach (var (_, unitShow) in _unitShows) {
            unitShow.QueueFree();
        }
        _unitShows.Clear();
        UnitsInSceneRes.RemoveAll();
    }

    /// <summary>服务事件：单位生命值变化。血条直读 Pawn.Health，事件仅做表现响应钩子。</summary>
    private void OnUnitHealth(ushort netId, float newHealth, float oldHealth) {
    }

    /// <summary>服务事件：单位死亡（主线程直接同步隐藏）。</summary>
    private void OnUnitDied(ushort netId) {
        if (_unitShows.TryGetValue(netId, out var show)) {
            show.Visible = false;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Unit died: netId={NetId}", netId);
        }
    }

    /// <summary>服务事件：单位添加 Buff（当前无展示行为）。</summary>
    private void OnBuffAdded(ushort netId, BuffView buff) {
    }

    /// <summary>服务事件：单位移除 Buff（当前无展示行为）。</summary>
    private void OnBuffRemoved(ushort netId, BuffView buff) {
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

    /// <summary>解析目标阵营相对本地玩家的关系；本地单位或关系函数未就绪返回 false。</summary>
    public bool TryResolveLocalCampRelation(string targetCamp, out CampRelation relation) {
        var relations = RelationsOrResolve();
        var localPawn = _roomClient?.LocalUnitPawn;
        if (relations == null || localPawn == null) {
            relation = default;
            return false;
        }
        relation = relations.Invoke([localPawn.Camp.Value], [targetCamp]);
        return true;
    }

    /// <summary>Running 阶段就绪校验：战斗已开始仍无阵营判定能力属时序故障，响亮报告。</summary>
    public void AssertCampRelationsReady() {
        if (_relations == null) {
            _logger.LogError(
                "[BattleUnitManager] 战斗 Running 阶段阵营关系仍未装配（DungeonKey 未同步），技能预拦将被拒绝。");
        }
    }

    /// <summary>收集与本地单位阵营敌对且存活（UnitState==0）的单位，按场景单位顺序排列。</summary>
    private List<UnitPawn> GetLivingEnemies(UnitPawn self) {
        List<UnitPawn> enemies = [];
        foreach (var candidate in UnitsArr) {
            if (candidate.Id == self.Id || candidate.UnitState.Value == 1)
                continue;
            if (!TryResolveLocalCampRelation(candidate.Camp.Value, out var relation)
                || relation != CampRelation.Enemy)
                continue;
            enemies.Add(candidate);
        }
        return enemies;
    }
}
