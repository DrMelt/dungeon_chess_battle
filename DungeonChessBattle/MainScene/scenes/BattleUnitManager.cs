using System;
using System.Collections.Generic;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GameAssets;
using DungeonChessBattle.GamePanels;
using DungeonChessBattle.GamePlayUI;
using DungeonChessBattle.Services;
using BuffView = DungeonChessBattle.Battle.Domain.Combat.BuffView;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 战斗单位管理器：单位视图（UnitGameShow）的全生命周期所有者。
/// 负责订阅单位服务事件、幂等生成/销毁视图。
/// 展示数据源为网络同步 UnitPawn（直读 SyncVar），本组件仅在单位创建/死亡/血量变化时做视图表现处理。
/// 由 MainScene 在进入/退出战斗时 Bind/Unbind。
/// </summary>
public partial class BattleUnitManager : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleUnitManager> _logger = ServiceLocator.GetLogger<BattleUnitManager>();

    /// <summary>单位展示场景（unit_game_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    /// <summary>玩家界面资源引用，桥接服务端聚焦目标到 UI 选中态。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceResRef;

    /// <summary>场景单位集合资源（UI 层数据源：状态条、技能目标、计时等）。</summary>
    public UnitsInScene UnitsInSceneRes { get; } = new();

    /// <summary>场景单位 Pawn 数组快照。</summary>
    public List<UnitPawn> UnitsArr => UnitsInSceneRes.UnitsArr;

    /// <summary>当前战斗服务（Bind 时注入，用于事件订阅）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>当前战斗服务引用（供技能链发起施法 RPC）。</summary>
    public IClientBattleService? BattleService => _battleService;

    /// <summary>当前房间 ID（供技能链发起施法 RPC）。</summary>
    public string RoomId => _roomId;

    /// <summary>本地玩家控制的单位视图就绪事件，参数为对应的 UnitGameShow。</summary>
    public event Action<UnitGameShow>? LocalUnitShowReady;

    /// <summary>本地玩家控制的单位视图，控制器或视图未就绪时返回 null。</summary>
    public UnitGameShow? LocalUnitShow {
        get {
            var pawn = _roomClient?.LocalUnitPawn;
            return pawn != null ? _unitShows.GetValueOrDefault(pawn.Id) : null;
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

        service.OnUnitCreated += OnServiceUnitCreated;
        service.UnitHealthChanged += OnUnitHealth;
        service.UnitDied += OnUnitDied;
        service.UnitBuffAdded += OnBuffAdded;
        service.UnitBuffRemoved += OnBuffRemoved;
        service.UnitFocusTargetChanged += OnUnitFocusTargetChanged;

        // 注入服务端权威房间创建时间（跨端一致的战斗计时起点）
        var createdUnix = service.GetRoomCreatedUnixTime(roomId);
        if (createdUnix is > 0) {
            UnitsInSceneRes.SetRoomCreatedAt(
                DateTimeOffset.FromUnixTimeSeconds(createdUnix.Value).UtcDateTime);
        }

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
            _battleService.UnitFocusTargetChanged -= OnUnitFocusTargetChanged;
        }

        ClearUnits();

        // 重置战斗计时起点，避免跨房间串扰
        UnitsInSceneRes.SetRoomCreatedAt(DateTime.MinValue);

        _battleService = null;
        _roomId = "";
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

        // 按显示名取配置（技能资源构建来源）
        var entry = UnitCatalog.GetByDisplayName(unitName);

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        // 注入网络同步 Pawn（setter 先于挂载，_Ready 校验不会误报）
        unitShow.Pawn = pawn;

        // 从配置构建 Godot 技能资源列表（独立于网络技能模型）
        if (entry != null) {
            foreach (var skillConfig in entry.Config.Skills) {
                unitShow.SkillsList.Add(SkillResourceTable.LoadResource(skillConfig));
            }
        }

        UnitsInSceneRes.AddUnit(pawn);
        AddChild(unitShow);
        _unitShows[pawn.Id] = unitShow;

        // 本地玩家单位的视图就绪通知，供 UI 层自动展示自身状态与技能
        if (pawn == _roomClient?.LocalUnitPawn) {
            LocalUnitShowReady?.Invoke(unitShow);
            // 重连场景初始同步的聚焦目标不触发 BindOnChange，主动桥接一次
            OnUnitFocusTargetChanged(pawn.Id, pawn.FocusTargetNetId.Value);
        }

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
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("SetLocalFocusTarget: selector={UnitId} -> target={TargetId}",
                pawn.Id, targetNetId);
    }

    /// <summary>
    /// 服务端聚焦目标变化桥接：仅本地玩家单位的聚焦变化映射为 UI 选中态。
    /// 目标单位视图未生成时置空选中，随后续单位生成事件补全。
    /// </summary>
    private void OnUnitFocusTargetChanged(ushort unitNetId, ushort targetNetId) {
        var localPawn = _roomClient?.LocalUnitPawn;
        if (localPawn == null || localPawn.Id != unitNetId || playerInterfaceResRef == null) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug(
                    "Focus sync ignored: caller={CallerId}, localPawn={LocalPawnId}, hasUi={HasUi}, target={TargetId}",
                    unitNetId, localPawn?.Id ?? 0, playerInterfaceResRef != null, targetNetId);
            return;
        }

        var targetShow = targetNetId != 0 ? _unitShows.GetValueOrDefault(targetNetId) : null;
        playerInterfaceResRef.FocusOnUnit = targetShow;
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Focus synced: selector={UnitId} -> {TargetId}, showExists={HasShow}",
                unitNetId, targetNetId, targetShow != null);
    }
}
