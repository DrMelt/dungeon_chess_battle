using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 物理持续伤害（DOT）Buff。
/// </summary>
[GlobalClass]
public partial class Buff_DOT_Physcial : BuffBaseGodot {
    /// <summary>指向物理 DOT 的领域 Buff 定义。</summary>
    protected override BuffDefinition Config => GameConfigDB.BuffDotPhysical;
}
