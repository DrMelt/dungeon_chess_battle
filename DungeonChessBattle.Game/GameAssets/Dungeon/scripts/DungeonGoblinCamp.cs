using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = GameConfig.Models.DungeonConfig;

/// <summary>哥布林营地副本资源。</summary>
[GlobalClass]
public partial class DungeonGoblinCamp : DungeonResourceBaseGodot {
    /// <inheritdoc/>
    protected override DungeonConfigDef Config => GameConfigDB.DungeonGoblinCamp;
}
