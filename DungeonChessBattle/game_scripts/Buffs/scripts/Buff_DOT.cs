using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Buff_DOT : BuffBaseGodot {
    protected override BuffConfig Config => GameConfigDB.BuffDotMagic;
}
