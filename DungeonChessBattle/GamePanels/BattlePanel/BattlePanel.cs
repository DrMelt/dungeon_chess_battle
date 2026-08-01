using System.Collections.Generic;
using System.Linq;
using Godot;
using DungeonChessBattle.Client;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using SysNumerics = System.Numerics;

namespace DungeonChessBattle;

/// <summary>
/// 战斗面板，负责桥接 LES Entity 数据与 Godot 3D 渲染。
/// 接收 GameLobby.BattleStarted 信号后进入战斗。
/// 每帧收集玩家输入并通过 LES 输入系统发送到服务端（预测+权威）。
/// </summary>
public partial class BattlePanel : Control {
    #region Signals

    [Signal]
    public delegate void BattleEndedEventHandler();

    #endregion

    #region References

    [Export]
    private DungeonEnv? _dungeonEnv;

    public BattlePanelInterRefs? InterRefs {
        get; private set;
    }

    #endregion

    #region State

    private RoomBattleClient? _roomClient;
    private string _roomId = "";

    /// <summary>UnitStateName → UnitGameShow 映射</summary>
    private readonly Dictionary<string, UnitGameShow> _unitShows = [];

    private Vector2 _moveDir;
    private byte _skillFlags;
    private Vector2 _aimPos;

    #endregion

    public override void _Ready() {
        InterRefs = GetNode<BattlePanelInterRefs>("BattlePanelInterRefs");
        if (InterRefs is null) {
            GD.PrintErr("[BattlePanel] BattlePanelInterRefs node not found.");
            return;
        }

        InterRefs?.BackButton?.Pressed += ExitBattle;
        Visible = false;
    }

    // =============================================================
    // 进入/退出战斗
    // =============================================================

    public void EnterBattle(string roomId, RoomBattleClient roomClient) {
        _roomId = roomId;
        _roomClient = roomClient;

        GD.Print($"[BattlePanel] Entering battle for room: {roomId}");

        _roomClient.UnitHealthChanged += OnUnitHealth;
        _roomClient.UnitDied += OnUnitDied;
        _roomClient.UnitBuffAdded += OnBuffAdded;
        _roomClient.UnitBuffRemoved += OnBuffRemoved;
        _roomClient.BattlePhaseChanged += OnBattlePhase;

        InitializeUnitsFromCache();

        if (InterRefs?.UserUI != null && InterRefs?.UnitsShow != null)
            InterRefs.UserUI.UpdateBinding();

        if (InterRefs?.BattleCamera != null)
            InterRefs.BattleCamera.Current = true;

        Visible = true;
        InterRefs?.StatusLabel?.Text = "战斗中...";
    }

    public void ExitBattle() {
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

        Visible = false;
        EmitSignal(SignalName.BattleEnded);
        GD.Print("[BattlePanel] Exited battle.");
    }

    // =============================================================
    // 从 LES Entity 缓存初始化 3D 单位
    // =============================================================

    private void InitializeUnitsFromCache() {
        if (_roomClient == null)
            return;

        var room = _roomClient.GetRoom(_roomId);
        if (room == null) {
            GD.PrintErr("[BattlePanel] Room not found in cache: " + _roomId);
            return;
        }

        GD.Print($"[BattlePanel] Initializing units: CampA={room.UnitsA.Count}, CampB={room.UnitsB.Count}");

        foreach (var unit in room.UnitsA)
            SpawnUnit(unit, EnumCamp.Camp_A);
        foreach (var unit in room.UnitsB)
            SpawnUnit(unit, EnumCamp.Camp_B);
    }

    private void SpawnUnit(IUnitState unit, EnumCamp camp) {
        if (InterRefs?.UnitShowScene == null || InterRefs?.UnitsShow == null)
            return;

        var spawnPoint = camp == EnumCamp.Camp_A ? InterRefs.CampAStart : InterRefs.CampBStart;
        Vector3 spawnPos = spawnPoint?.SamplePosition() ?? Vector3.Zero;

        var unitShow = InterRefs.UnitShowScene.Instantiate<UnitGameShow>();
        if (unitShow == null) {
            GD.PrintErr("[BattlePanel] Failed to instantiate UnitShowScene.");
            return;
        }

        var unitState = unitShow.UnitStateRec;
        if (unitState == null) {
            GD.PrintErr("[BattlePanel] UnitShowScene has no UnitStateRec.");
            unitShow.QueueFree();
            return;
        }

        // IUnitState → UnitState (Godot Resource)
        unitState.UnitStateName = unit.UnitStateName;
        unitState.Camp = unit.Camp;
        unitState.Health = unit.Health;

        unitShow.SetUnitGlobalPosition(spawnPos);
        unitShow.SetUnitGlobalDir(Vector3.Forward);

        InterRefs.UnitsShow.AddUnitShow(unitShow);
        _unitShows[unit.UnitStateName] = unitShow;

        GD.Print($"[BattlePanel] Spawned unit '{unit.UnitStateName}' at {spawnPos}");
    }

    private void ClearUnits() {
        if (InterRefs?.UnitsShow == null)
            return;

        foreach (var (_, unitShow) in _unitShows) {
            unitShow.QueueFree();
        }
        _unitShows.Clear();
    }

    // =============================================================
    // Godot 帧循环：输入 + 位置同步
    // =============================================================

    public override void _Process(double delta) {
        if (_roomClient == null || !Visible)
            return;

        CollectPlayerInput();

        _roomClient.SubmitPlayerInput(
            new SysNumerics.Vector2(_moveDir.X, _moveDir.Y),
            _skillFlags,
            new SysNumerics.Vector2(_aimPos.X, _aimPos.Y));
    }

    public override void _PhysicsProcess(double delta) {
        if (_roomClient == null || !Visible)
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
                // IUnitState.Position (System.Numerics.Vector3) → Godot.Vector3
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
        var battlePhase = (BattlePhase)phase;
        InterRefs?.StatusLabel?.Text = $"战斗阶段: {battlePhase}";

        if (battlePhase == BattlePhase.Finished) {
            GD.Print("[BattlePanel] Battle finished detected via LES sync.");
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
            GD.Print($"[BattlePanel] Unit died: {unitName}");
        }
    }

    private void OnBuffAdded(string unitName, Entities.SyncData.SyncBuffData buff) {
    }

    private void OnBuffRemoved(string unitName, Entities.SyncData.SyncBuffData buff) {
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
