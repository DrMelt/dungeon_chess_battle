using Godot;
using DungeonChessBattle.Client;
using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle;

/// <summary>
/// 战斗场景初始化器。
/// 监听 GameLobby 的 BattleStarted 信号，动态实例化战斗单位并开始战斗。
/// 替换 TestInit 的硬编码初始化方式。
/// </summary>
public partial class BattleSceneInitializer : Node {
    [Export] private GameLobby _gameLobby = null!;
    [Export] private UnitsInScene_Show _unitsInSceneShow = null!;
    [Export] private CampStartPoints _campAStartPoint = null!;
    [Export] private CampStartPoints _campBStartPoint = null!;
    [Export] private PackedScene _unitShowScene = null!;

    public override void _Ready() {
        if (_gameLobby == null) {
            GD.PrintErr("[BattleSceneInitializer] _gameLobby not assigned!");
            return;
        }

        _gameLobby.BattleStarted += OnBattleStarted;
    }

    private void OnBattleStarted(string roomId) {
        var clientService = _gameLobby.ClientService;
        if (clientService == null) {
            GD.PrintErr("[BattleSceneInitializer] No client service available!");
            return;
        }

        GD.Print($"[BattleSceneInitializer] Battle started for room: {roomId}");

        var gameRoom = clientService.GetRoom(roomId);
        if (gameRoom == null) {
            GD.PrintErr($"[BattleSceneInitializer] Room {roomId} not found!");
            return;
        }

        int spawned = 0;
        foreach (var unitModel in gameRoom.UnitsA) {
            if (SpawnUnit(unitModel.UnitStateName, EnumCamp.Camp_A, _campAStartPoint))
                spawned++;
        }
        foreach (var unitModel in gameRoom.UnitsB) {
            if (SpawnUnit(unitModel.UnitStateName, EnumCamp.Camp_B, _campBStartPoint))
                spawned++;
        }

        GD.Print($"[BattleSceneInitializer] Spawned {spawned} units.");
    }

    private bool SpawnUnit(string unitName, EnumCamp camp, CampStartPoints startPoint) {
        var unitState = CreateUnitState(unitName);
        if (unitState == null) {
            GD.PrintErr($"[BattleSceneInitializer] Unknown unit type: {unitName}");
            return false;
        }

        unitState.Camp = camp;
        unitState.UnitStateName = unitName;

        var unitShow = _unitShowScene.Instantiate<UnitGameShow>();
        unitShow.UnitStateRec = unitState;

        var spawnPos = startPoint.SamplePosition();
        unitShow.SetUnitGlobalPosition(spawnPos);

        _unitsInSceneShow.AddUnitShow(unitShow);

        GD.Print($"[BattleSceneInitializer] Spawned '{unitName}' camp={camp} pos={spawnPos}");
        return true;
    }

    /// <summary>
    /// 根据单位名称创建对应的 UnitState 实例。
    /// 使用正确覆写 Config 的子类来确保 EnsureSynced() 正常运作。
    /// </summary>
    private static UnitState? CreateUnitState(string unitName) => unitName switch {
        "White Mage" => new Unit_WhiteMage(),
        _ => null,
    };
}