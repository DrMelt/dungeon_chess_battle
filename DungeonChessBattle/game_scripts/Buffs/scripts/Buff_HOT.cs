using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Buff_HOT : BuffBaseGodot {
    protected override BuffConfig Config => GameConfigDB.BuffHot;
}
