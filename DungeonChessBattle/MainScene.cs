using System.Collections.Generic;
using System.Linq;
using Godot;
using DungeonChessBattle.Client;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Services;
using SysNumerics = System.Numerics;

namespace DungeonChessBattle;

/// <summary>
/// 主场景入口脚本，挂载到 MainScene 根节点。
/// 负责初始化服务、处理战斗循环（输入收集 + LES Entity 位置同步）。
/// </summary>
public partial class MainScene : Node {
    #region Signals

    [Signal]
    public delegate void BattleEndedEventHandler();

    #endregion

    #region Exports

    [Export]
    private MainMenu? _mainMenu;

    [Export]
    private GameLobby? _gameLobby;

    [Export]
    private UnitsInScene_Show? _unitsShow;

    [Export]
    private DungeonEnv? _dungeonEnv;

    [Export]
    private Node2d_UserUI? _userUI;

    [Export]
    private PackedScene? _unitShowScene;

    #endregion

    #region State

    private RoomBattleClient? _roomClient;
    private string _roomId = "";
    private bool _inBattle;

    /// <summary>UnitStateName → UnitGameShow 映射</summary>
    private readonly Dictionary<string, UnitGameShow> _unitShows = [];

    private Vector2 _moveDir;
    private byte _skillFlags;
    private Vector2 _aimPos;

    #endregion

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
        if (_dungeonEnv == null)
            GD.PrintErr("[MainScene] [Export] _dungeonEnv is not assigned!");
        if (_userUI == null)
            GD.PrintErr("[MainScene] [Export] _userUI is not assigned!");
        if (_unitShowScene == null)
            GD.PrintErr("[MainScene] [Export] _unitShowScene is not assigned!");
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    private void OnBattleStarted(string roomId) {
        _roomId = roomId;
        _roomClient = ServiceLocator.ClientService.RoomClient;

        GD.Print($"[MainScene] Battle started for room: {roomId}");

        // 订阅 LES 事件
        _roomClient.UnitHealthChanged += OnUnitHealth;
        _roomClient.UnitDied += OnUnitDied;
        _roomClient.UnitBuffAdded += OnBuffAdded;
        _roomClient.UnitBuffRemoved += OnBuffRemoved;
        _roomClient.BattlePhaseChanged += OnBattlePhase;

        // 从 LES Entity 缓存初始化 3D 单位
        InitializeUnitsFromCache();

        // 绑定 UI
        _userUI?.UpdateBinding();

        _gameLobby!.Visible = false;
        _inBattle = true;

        GD.Print("[MainScene] Entered battle.");
    }

    private void ExitBattle() {
        if (_roomClient != null) {
            _roomClient.UnitHealthChanged -= OnUnitHealth;
            _roomClient.UnitDied -= OnUnitDied;
            _roomClient.UnitBuffAdded -= OnBuffAdded;
            _roomClient.UnitBuffRemoved -= OnBuffRemoved;
            _roomClient.BattlePhaseChanged -= OnBattlePhase;
        }

        ClearUnits();

        _roomClient = null;
        _roomId = "";
        _unitShows.Clear();
        _inBattle = false;

        _gameLobby?.Visible = true;

        EmitSignal(SignalName.BattleEnded);
        GD.Print("[MainScene] Exited battle.");
    }

    // =============================================================
    // 单位管理
    // =============================================================

    private void InitializeUnitsFromCache() {
        if (_roomClient == null)
            return;

        var room = _roomClient.GetRoom(_roomId);
        if (room == null) {
            GD.PrintErr("[MainScene] Room not found in cache: " + _roomId);
            return;
        }

        GD.Print($"[MainScene] Initializing units: CampA={room.UnitsA.Count}, CampB={room.UnitsB.Count}");

        foreach (var unit in room.UnitsA)
            SpawnUnit(unit, EnumCamp.Camp_A);
        foreach (var unit in room.UnitsB)
            SpawnUnit(unit, EnumCamp.Camp_B);
    }

    private void SpawnUnit(IUnitState unit, EnumCamp camp) {
        if (_unitsShow == null)
            return;

        // 从 DungeonEnv 获取生成点
        CampStartPoints? spawnPoint = camp == EnumCamp.Camp_A
            ? _dungeonEnv?.GetNodeOrNull<CampStartPoints>("CampStartPoint_A")
            : _dungeonEnv?.GetNodeOrNull<CampStartPoints>("CampStartPoint_B");

        Vector3 spawnPos = spawnPoint?.SamplePosition() ?? Vector3.Zero;

        if (_unitShowScene == null)
            return;
        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null)
            return;

        var unitState = unitShow.UnitStateRec;
        // IUnitState → UnitState (Godot Resource)
        unitState.UnitStateName = unit.UnitStateName;
        unitState.Camp = unit.Camp;
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

    public override void _Process(double delta) {
        if (!_inBattle || _roomClient == null)
            return;

        CollectPlayerInput();

        _roomClient.SubmitPlayerInput(
            new SysNumerics.Vector2(_moveDir.X, _moveDir.Y),
            _skillFlags,
            new SysNumerics.Vector2(_aimPos.X, _aimPos.Y));
    }

    public override void _PhysicsProcess(double delta) {
        if (!_inBattle || _roomClient == null)
            return;

        SyncEntityPositionsToScene();
    }

    private void CollectPlayerInput() {
        float moveX = 0f;
        float moveY = 0f;
        if (Input.IsActionPressed("Move_Right"))
            moveX += 1f;
        if (Input.IsActionPressed("Move_Left"))
            moveX -= 1f;
        if (Input.IsActionPressed("Move_Up"))
            moveY += 1f;
        if (Input.IsActionPressed("Move_Down"))
            moveY -= 1f;

        var raw = new Vector2(moveX, moveY);
        if (raw.Length() > 1f)
            raw = raw.Normalized();
        _moveDir = raw;

        _skillFlags = 0;

        var mousePos = GetViewport().GetMousePosition();
        _aimPos = new Vector2(mousePos.X, mousePos.Y);
    }

    private void SyncEntityPositionsToScene() {
        if (_roomClient == null)
            return;

        var room = _roomClient.GetRoom(_roomId);
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
    // LES Entity 事件回调
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

    private void OnBuffAdded(string unitName, DungeonChessBattle.Entities.SyncData.SyncBuffData buff) {
    }
    private void OnBuffRemoved(string unitName, DungeonChessBattle.Entities.SyncData.SyncBuffData buff) {
    }

    public override void _ExitTree() {
        if (_roomClient != null) {
            _roomClient.UnitHealthChanged -= OnUnitHealth;
            _roomClient.UnitDied -= OnUnitDied;
            _roomClient.UnitBuffAdded -= OnBuffAdded;
            _roomClient.UnitBuffRemoved -= OnBuffRemoved;
            _roomClient.BattlePhaseChanged -= OnBattlePhase;
        }
    }
}
