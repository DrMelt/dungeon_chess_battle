using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GamePanels;
using DungeonChessBattle.Logic.Services;
using Godot;
using Godot.Collections;

namespace DungeonChessBattle;

/// <summary>
/// 战斗单位管理器：单位视图（UnitGameShow）的全生命周期所有者。
/// 负责订阅单位服务事件、幂等生成/销毁视图、每物理帧位置同步。
/// 由 MainScene 在进入/退出战斗时 Bind/Unbind。
/// </summary>
public partial class BattleUnitManager : Node {
    /// <summary>单位展示场景（unit_show.tscn）。</summary>
    [Export]
    private PackedScene? _unitShowScene;

    /// <summary>场景单位集合资源（UI 层数据源：状态条、技能目标、计时等）。</summary>
    public UnitsInScene UnitsInSceneRes { get; } = new();

    /// <summary>场景单位状态数组快照。</summary>
    public Array<UnitState> UnitsArr => UnitsInSceneRes.UnitsArr;

    /// <summary>当前战斗服务（Bind 时注入）。</summary>
    private IClientBattleService? _battleService;

    /// <summary>当前房间 ID（Bind 时注入）。</summary>
    private string _roomId = "";

    /// <summary>UnitStateName → UnitGameShow 映射。</summary>
    private readonly System.Collections.Generic.Dictionary<string, UnitGameShow> _unitShows = [];

    /// <summary>
    /// 进入战斗：注入服务与房间 ID，订阅单位事件并初始化缓存单位。
    /// </summary>
    public void Bind(IClientBattleService service, string roomId) {
        _battleService = service;
        _roomId = roomId;

        service.OnUnitCreated += OnServiceUnitCreated;
        service.UnitHealthChanged += OnUnitHealth;
        service.UnitDied += OnUnitDied;
        service.UnitBuffAdded += OnBuffAdded;
        service.UnitBuffRemoved += OnBuffRemoved;

        InitializeUnitsFromCache();
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
        _roomId = "";
    }

    /// <summary>
    /// 每物理帧同步实体位置到 3D 场景。
    /// </summary>
    public void SyncPositions() {
        if (_battleService == null)
            return;

        var room = _battleService.GetRoom(_roomId);
        if (room == null)
            return;

        foreach (var unit in room.UnitsA.Concat(room.UnitsB)) {
            if (_unitShows.TryGetValue(unit.UnitStateName, out var show)) {
                var pos = unit.Position;
                GD.Print($"[MainScene] Sync {unit.UnitStateName}: gamePos=({pos.X},{pos.Z})");
                show.SetUnitGlobalPosition(new Vector3(pos.X, 0, pos.Z));
            }
        }
    }

    /// <summary>节点退出场景树：兜底退订（防止战斗中途场景被释放导致事件悬挂）。</summary>
    public override void _ExitTree() {
        Unbind();
    }

    /// <summary>
    /// 服务事件：单位创建。网络模式下单位实体可能晚于战斗开始到达；
    /// 与 InitializeUnitsFromCache 缓存兜底共用幂等入口，保证不重不漏。
    /// CallDeferred 保证在帧回调之外挂载场景树节点。
    /// </summary>
    private void OnServiceUnitCreated(string eventRoomId, string unitName, string camp) {
        if (eventRoomId != _roomId)
            return;
        GD.Print($"[MainScene] Unit created via service: {unitName} (camp={camp})");

        // 从房间缓存取完整模型（含位置/属性），事件携带的 camp 不参与生成
        if (_battleService == null)
            return;
        var room = _battleService.GetRoom(_roomId);
        if (room == null)
            return;
        CallDeferred(nameof(SpawnUnitFromCache), unitName);
    }

    /// <summary>延迟生成单位（CallDeferred 入口）。</summary>
    private void SpawnUnitFromCache(string unitName) {
        if (_battleService == null)
            return;
        var room = _battleService.GetRoom(_roomId);
        if (room == null)
            return;
        var unit = room.UnitsA.Concat(room.UnitsB)
            .FirstOrDefault(u => u.UnitStateName == unitName);
        if (unit != null)
            TrySpawnUnit(unit);
    }

    /// <summary>
    /// 幂等生成单位视图：同名单位已存在时跳过。
    /// 事件驱动路径（OnServiceUnitCreated）与缓存兜底路径（InitializeUnitsFromCache）共用，
    /// 保证订阅前已存在实体与订阅后新建实体均被生成且不重复。
    ///　</summary>
    private void TrySpawnUnit(IUnitState unit) {
        if (_unitShows.ContainsKey(unit.UnitStateName))
            return;
        SpawnUnit(unit);
    }

    private void SpawnUnit(IUnitState unit) {
        // 按显示名从唯一权威注册表取运行时资源工厂（unit_show.tscn 不携带单位资源）
        var entry = UnitCatalog.GetByDisplayName(unit.UnitStateName);
        if (entry == null) {
            GD.PushWarning($"[MainScene] SpawnUnit: unit '{unit.UnitStateName}' not found in UnitCatalog.");
            return;
        }
        var unitState = entry.StateFactory();

        Vector3 spawnPos = new(unit.Position.X, 0, unit.Position.Z);

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        // 注入运行时资源（setter 先于挂载，_Ready 校验不会误报）
        unitShow.UnitStateRec = unitState;
        CopyRuntimeFields(unit, unitState);

        unitShow.SetUnitGlobalPosition(spawnPos);
        unitShow.SetUnitGlobalDir(Vector3.Forward);

        UnitsInSceneRes.AddUnit(unitState);
        AddChild(unitShow);
        _unitShows[unit.UnitStateName] = unitShow;

        GD.Print($"[MainScene] Spawned unit '{unit.UnitStateName}' at {spawnPos}");
    }

    /// <summary>将网络同步单位模型的运行时字段搬运到展示资源。</summary>
    private static void CopyRuntimeFields(IUnitState source, UnitState target) {
        target.UnitStateName = source.UnitStateName;
        target.Camps.Clear();
        target.Camps.AddRange(source.Camps);
        target.Health = source.Health;
    }

    private void InitializeUnitsFromCache() {
        if (_battleService == null)
            return;

        var room = _battleService.GetRoom(_roomId);
        if (room == null) {
            GD.PrintErr("[MainScene] Room not found in cache: " + _roomId);
            return;
        }

        GD.Print($"[MainScene] Initializing units: CampA={room.UnitsA.Count}, CampB={room.UnitsB.Count}");

        foreach (var unit in room.UnitsA)
            TrySpawnUnit(unit);
        foreach (var unit in room.UnitsB)
            TrySpawnUnit(unit);
    }

    private void ClearUnits() {
        foreach (var (_, unitShow) in _unitShows) {
            unitShow.QueueFree();
        }
        _unitShows.Clear();
    }

    /// <summary>服务事件：单位生命值变化（主线程直接同步写入）。</summary>
    private void OnUnitHealth(string unitName, float newHealth, float oldHealth) {
        if (_unitShows.TryGetValue(unitName, out var show)) {
            show.UnitStateRec.Health = newHealth;
        }
    }

    /// <summary>服务事件：单位死亡（主线程直接同步隐藏）。</summary>
    private void OnUnitDied(string unitName) {
        if (_unitShows.TryGetValue(unitName, out var show)) {
            show.Visible = false;
            GD.Print($"[MainScene] Unit died: {unitName}");
        }
    }

    /// <summary>服务事件：单位添加 Buff（当前无展示行为）。</summary>
    private void OnBuffAdded(string unitName, BuffEventData buff) {
    }

    /// <summary>服务事件：单位移除 Buff（当前无展示行为）。</summary>
    private void OnBuffRemoved(string unitName, BuffEventData buff) {
    }
}
