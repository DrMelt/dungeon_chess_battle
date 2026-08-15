using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.GameAssets;

using DungeonConfigDef = DungeonChessBattle.GameConfig.Models.DungeonConfig;

/// <summary>深邃洞窟副本资源。</summary>
[GlobalClass]
public partial class DungeonDeepCave : DungeonResourceBaseGodot {
    /// <inheritdoc/>
    protected override DungeonConfigDef Config => GameConfigDB.DungeonDeepCave;
}
