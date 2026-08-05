using System.Collections.Generic;
using System.Linq;
using Godot;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Services;

namespace DungeonChessBattle;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责初始化服务、处理战斗循环（输入收集 + LES Entity 位置同步）。
/// 全部通过 IClientBattleService 接口消费服务，不再依赖 RoomBattleClient 具体类型。
/// </summary>
public partial class MainScene : Node {
    #region Signals

    /// <summary>战斗结束信号。</summary>
    [Signal]
    public delegate void BattleEndedEventHandler();

    #endregion

    #region Exports

    [Export]
    private MainMenu? _mainMenu;

    [Export]
    private GameLobby? _gameLobby;

    [Export]
    private UnitsInSceneView? _unitsShow;

    [Export]
    private PlayerOperationInterfaceInfo? _playerOperationInterfaceInfo;

    [Export]
    private PackedScene? _unitShowScene;

    #endregion

    #region State

    /// <summary>战斗服务（接口类型，通过 ServiceLocator 获取）。唯一服务引用。</summary>
    private IClientBattleService? _battleService;

    private string _roomId = "";
    private bool _inBattle;

    /// <summary>UnitStateName → UnitGameShow 映射</summary>
    private readonly Dictionary<string, UnitGameShow> _unitShows = [];

    private Vector2 _moveDir;
    private byte _skillFlags;
    private Vector2 _aimPos;

    #endregion

    /// <summary>
    /// 节点就绪：校验导出引用、订阅战斗开始信号并显示主菜单。
    /// </summary>
    public override void _Ready() {
        ValidateExports();

        // 连接 GameLobby 的 BattleStarted 信号
        _gameLobby?.BattleStarted += OnBattleStarted;

        // 默认显示主菜单
        _mainMenu?.OpenPanelFrom();

        GD.Print("[MainScene] _Ready Initialized.");
    }

    private void ValidateExports() {
        if (_mainMenu == null)
            GD.PrintErr("[MainScene] [Export] _mainMenu is not assigned!");
        if (_gameLobby == null)
            GD.PrintErr("[MainScene] [Export] _gameLobby is not assigned!");
        if (_unitsShow == null)
            GD.PrintErr("[MainScene] [Export] _unitsShow is not assigned!");
        if (_playerOperationInterfaceInfo == null)
            GD.PrintErr("[MainScene] [Export] _playerOperationInterfaceInfo is not assigned!");
        if (_unitShowScene == null)
            GD.PrintErr("[MainScene] [Export] _unitShowScene is not assigned!");
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    private void OnBattleStarted(string roomId) {
        _roomId = roomId;
        _battleService = ServiceLocator.ClientService.Client;

        GD.Print($"[MainScene] Battle started for room: {roomId}");

        // 所有事件均通过接口订阅
        if (_battleService != null) {
            _battleService.BattlePhaseChanged += OnBattlePhase;
            _battleService.OnUnitCreated += OnServiceUnitCreated;
            _battleService.UnitHealthChanged += OnUnitHealth;
            _battleService.UnitDied += OnUnitDied;
            _battleService.UnitBuffAdded += OnBuffAdded;
            _battleService.UnitBuffRemoved += OnBuffRemoved;
        }

        // 从 Entity 缓存初始化 3D 单位
        InitializeUnitsFromCache();

        // 绑定 UI（通过 VM 触发 View 初始化）
        _playerOperationInterfaceInfo?.BindToBattle();

        _gameLobby?.Visible = false;
        _inBattle = true;

        GD.Print("[MainScene] Entered battle.");
    }

    private void ExitBattle() {
        if (_battleService != null) {
            _battleService.BattlePhaseChanged -= OnBattlePhase;
            _battleService.OnUnitCreated -= OnServiceUnitCreated;
            _battleService.UnitHealthChanged -= OnUnitHealth;
            _battleService.UnitDied -= OnUnitDied;
            _battleService.UnitBuffAdded -= OnBuffAdded;
            _battleService.UnitBuffRemoved -= OnBuffRemoved;
        }

        ClearUnits();

        // 解绑 UI
        _playerOperationInterfaceInfo?.UnbindFromBattle();

        _battleService = null;
        _roomId = "";
        _unitShows.Clear();
        _inBattle = false;

        _gameLobby?.Visible = true;

        EmitSignal(SignalName.BattleEnded);
        GD.Print("[MainScene] Exited battle.");
    }

    /// <summary>接口事件：服务端确认单位创建（网络模式异步，本地模式同步）。</summary>
    private void OnServiceUnitCreated(string eventRoomId, string unitName, string camp) {
        if (eventRoomId != _roomId)
            return;
        GD.Print($"[MainScene] Unit created via service: {unitName} (camp={camp})");
    }

    // =============================================================
    // 单位管理
    // =============================================================

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
            SpawnUnit(unit);
        foreach (var unit in room.UnitsB)
            SpawnUnit(unit);
    }

    private void SpawnUnit(IUnitState unit) {
        if (_unitsShow == null)
            return;

        // 出生位置由 LES 同步，直接从 IUnitState.Position 获取
        Vector3 spawnPos = new(unit.Position.X, 0, unit.Position.Y);

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        var unitState = unitShow.UnitStateRec;
        // IUnitState → UnitState (Godot Resource)
        unitState.UnitStateName = unit.UnitStateName;
        unitState.Camps.Clear();
        unitState.Camps.AddRange(unit.Camps);
        unitState.Health = unit.Health;

        unitShow.SetUnitGlobalPosition(spawnPos);
        unitShow.SetUnitGlobalDir(Vector3.Forward);

        _unitsShow.AddUnitShow(unitShow);
        _unitShows[unit.UnitStateName] = unitShow;

        GD.Print($"[MainScene] Spawned unit '{unit.UnitStateName}' at {spawnPos}");
    }

    private void ClearUnits() {
        foreach (var (_, unitShow) in _unitShows) {
            unitShow.QueueFree();
        }
        _unitShows.Clear();
    }

    // =============================================================
    // 帧循环：输入 + 位置同步
    // =============================================================

    /// <summary>
    /// 每帧处理战斗输入收集并提交到战斗服务。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        if (!_inBattle || _battleService == null)
            return;

        CollectPlayerInput();

        _battleService.SubmitPlayerInput(_moveDir.X, _moveDir.Y, _skillFlags, _aimPos.X, _aimPos.Y);
    }

    /// <summary>
    /// 每物理帧同步实体位置到 3D 场景。
    /// </summary>
    /// <param name="delta">距上一物理帧的秒数。</param>
    public override void _PhysicsProcess(double delta) {
        if (!_inBattle || _battleService == null)
            return;

        SyncEntityPositionsToScene();
    }

    private void CollectPlayerInput() {
        // UI 阻塞时跳过战斗输入收集（等待技能/移动目标选择中）
        if (_playerOperationInterfaceInfo?.IsBlockingInput == true)
            return;

        _moveDir = Input.GetVector("Move_Left", "Move_Right", "Move_Up", "Move_Down");

        _skillFlags = 0;

        var mousePos = GetViewport().GetMousePosition();
        _aimPos = new Vector2(mousePos.X, mousePos.Y);
    }

    private void SyncEntityPositionsToScene() {
        if (_battleService == null)
            return;

        var room = _battleService.GetRoom(_roomId);
        if (room == null)
            return;

        foreach (var unit in room.UnitsA.Concat(room.UnitsB)) {
            if (_unitShows.TryGetValue(unit.UnitStateName, out var show)) {
                var pos = unit.Position;
                show.SetUnitGlobalPosition(new Vector3(pos.X, 0, pos.Y));
            }
        }
    }

    // =============================================================
    // IClientBattleService 事件回调
    // =============================================================

    private void OnBattlePhase(string roomId, BattlePhase phase) {
        if (roomId != _roomId)
            return;
        CallDeferred(nameof(DeferredBattlePhase), (int)phase);
    }

    private void DeferredBattlePhase(int phase) {
        if (phase == (int)BattlePhase.Finished) {
            GD.Print("[MainScene] Battle finished detected via LES sync.");
            CallDeferred(nameof(ExitBattle));
        }
    }

    private void OnUnitHealth(string unitName, float newHealth, float oldHealth) {
        CallDeferred(nameof(DeferredUnitHealth), unitName, newHealth);
    }

    private void DeferredUnitHealth(string unitName, float newHealth) {
        if (_unitShows.TryGetValue(unitName, out var show)) {
            show.UnitStateRec.Health = newHealth;
        }
    }

    private void OnUnitDied(string unitName) {
        CallDeferred(nameof(DeferredUnitDied), unitName);
    }

    private void DeferredUnitDied(string unitName) {
        if (_unitShows.TryGetValue(unitName, out var show)) {
            show.Visible = false;
            GD.Print($"[MainScene] Unit died: {unitName}");
        }
    }

    private void OnBuffAdded(string unitName, BuffEventData buff) {
    }
    private void OnBuffRemoved(string unitName, BuffEventData buff) {
    }

    /// <summary>
    /// 节点退出场景树：取消战斗服务事件订阅。
    /// </summary>
    public override void _ExitTree() {
        if (_battleService != null) {
            _battleService.BattlePhaseChanged -= OnBattlePhase;
            _battleService.OnUnitCreated -= OnServiceUnitCreated;
            _battleService.UnitHealthChanged -= OnUnitHealth;
            _battleService.UnitDied -= OnUnitDied;
            _battleService.UnitBuffAdded -= OnBuffAdded;
            _battleService.UnitBuffRemoved -= OnBuffRemoved;
        }
    }
}
