using System.Collections.Generic;
using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GamePanels;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 单位展示管理器：单位视图（UnitGameShow）的全生命周期所有者。
/// 订阅服务端单位创建/死亡事件驱动视图生成与隐藏，并装配技能展示资源；
/// Pawn 数据投影与玩家操作归 BattleSessionContext，本组件不对外提供数据查询。
/// 由 MainScene 在进入/退出战斗时 Bind/Unbind。
/// </summary>
public partial class UnitShowManager : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UnitShowManager> _logger = ServiceLocator.GetLogger<UnitShowManager>();

    /// <summary>单位展示场景（unit_game_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    /// <summary>当前战斗服务（Bind 时注入，用于单位事件订阅）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>房间客户端（Bind 时注入，用于 Pawn 查询）。</summary>
    private RoomBattleClient? _roomClient;

    /// <summary>当前房间 ID（Bind 时注入，用于事件过滤）。</summary>
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
        service.UnitDied += OnUnitDied;

        InitializeUnitsFromPawns();
    }

    /// <summary>退出战斗：退订单位事件并清理全部单位视图。</summary>
    public void Unbind() {
        if (_battleService != null) {
            _battleService.OnUnitCreated -= OnServiceUnitCreated;
            _battleService.UnitDied -= OnUnitDied;
        }

        ClearUnits();

        _battleService = null;
        _roomClient = null;
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
    private void OnServiceUnitCreated(string eventRoomId, ushort netId, string unitName, IReadOnlyList<string> camps) {
        if (eventRoomId != _roomId)
            return;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Unit created via service: {UnitName} (camps={Camps}, netId={NetId})", unitName, string.Join(",", camps), netId);
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
    }

    /// <summary>服务事件：单位死亡（主线程直接同步隐藏）。</summary>
    private void OnUnitDied(ushort netId) {
        if (_unitShows.TryGetValue(netId, out var show)) {
            show.Visible = false;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("Unit died: netId={NetId}", netId);
        }
    }
}
