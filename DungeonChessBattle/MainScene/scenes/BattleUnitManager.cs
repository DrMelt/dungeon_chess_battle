using System;
using System.Collections.Generic;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Entities;
using DungeonChessBattle.GameAssets.Skills;
using DungeonChessBattle.GamePanels;
using DungeonChessBattle.Services;
using BuffView = DungeonChessBattle.Battle.Domain.Combat.BuffView;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle;

/// <summary>
/// 战斗单位管理器：单位视图（UnitGameShow）的全生命周期所有者。
/// 负责订阅单位服务事件、幂等生成/销毁视图。
/// 展示数据源为网络同步 UnitPawn（直读 SyncVar），本组件仅在单位创建/死亡/血量变化时做视图表现处理。
/// 由 MainScene 在进入/退出战斗时 Bind/Unbind。
/// </summary>
public partial class BattleUnitManager : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleUnitManager> _logger = ServiceLocator.GetLogger<BattleUnitManager>();

    /// <summary>单位展示场景（unit_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

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

    /// <summary>房间客户端（Bind 时注入，用于 Pawn 查询）。</summary>
    private RoomBattleClient? _roomClient;

    /// <summary>当前房间 ID（Bind 时注入）。</summary>
    private string _roomId = "";

    /// <summary>UnitStateName → UnitGameShow 映射。</summary>
    private readonly Dictionary<string, UnitGameShow> _unitShows = [];

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
    private void OnServiceUnitCreated(string eventRoomId, string unitName, string camp) {
        if (eventRoomId != _roomId)
            return;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[MainScene] Unit created via service: {UnitName} (camp={Camp})", unitName, camp);
        CallDeferred(nameof(SpawnUnitFromCache), unitName);
    }

    /// <summary>延迟生成单位（CallDeferred 入口）。</summary>
    private void SpawnUnitFromCache(string unitName) {
        var pawn = _roomClient?.FindPawnByName(unitName);
        if (pawn is not null)
            TrySpawnUnit(pawn);
        else
            _logger.LogWarning("[MainScene] Unit '{UnitName}' not found in pawn cache; entity may not have arrived yet", unitName);
    }

    /// <summary>
    /// 幂等生成单位视图：同名单位已存在时跳过。
    /// 事件驱动路径（OnServiceUnitCreated）与缓存兜底路径（InitializeUnitsFromPawns）共用。
    /// </summary>
    private void TrySpawnUnit(UnitPawn pawn) {
        if (_unitShows.ContainsKey(pawn.UnitName.Value))
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
        _unitShows[unitName] = unitShow;

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[MainScene] Spawned unit '{UnitName}' at {Position}", unitName, pawn.Position.Value);
    }

    private void InitializeUnitsFromPawns() {
        if (_roomClient == null)
            return;

        var pawns = _roomClient.GetPawns();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[MainScene] Initializing units: total={Total}", pawns.Count);

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
    private void OnUnitHealth(string unitName, float newHealth, float oldHealth) {
    }

    /// <summary>服务事件：单位死亡（主线程直接同步隐藏）。</summary>
    private void OnUnitDied(string unitName) {
        if (_unitShows.TryGetValue(unitName, out var show)) {
            show.Visible = false;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[MainScene] Unit died: {UnitName}", unitName);
        }
    }

    /// <summary>服务事件：单位添加 Buff（当前无展示行为）。</summary>
    private void OnBuffAdded(string unitName, BuffView buff) {
    }

    /// <summary>服务事件：单位移除 Buff（当前无展示行为）。</summary>
    private void OnBuffRemoved(string unitName, BuffView buff) {
    }
}
