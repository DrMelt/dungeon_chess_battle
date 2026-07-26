using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Unit_WhiteMage : UnitState {
    protected override UnitConfig Config => GameConfigDB.UnitWhiteMage;
}