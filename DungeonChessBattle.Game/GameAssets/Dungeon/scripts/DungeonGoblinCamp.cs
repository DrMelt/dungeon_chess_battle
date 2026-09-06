using DungeonChessBattle.Battle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = DungeonChessBattle.Battle.Shared.Content.DungeonConfig;

/// <summary>哥布林营地副本资源。</summary>
[GlobalClass]
public partial class DungeonGoblinCamp : DungeonResourceBaseGodot {
    /// <inheritdoc/>
    protected override DungeonConfigDef Config =>
        GameContentHost.Registry.GetRequiredDungeon("goblin_camp");
}
