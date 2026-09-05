using DungeonChessBattle.Battle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = DungeonChessBattle.Battle.GameConfig.Models.DungeonConfig;

/// <summary>深邃洞窟副本资源。</summary>
[GlobalClass]
public partial class DungeonDeepCave : DungeonResourceBaseGodot {
    /// <inheritdoc/>
    protected override DungeonConfigDef Config => GameConfigDB.DungeonDeepCave;
}
