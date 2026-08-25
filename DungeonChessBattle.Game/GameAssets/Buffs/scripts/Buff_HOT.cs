using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 持续治疗（HOT）Buff。
/// </summary>
[GlobalClass]
public partial class Buff_HOT : BuffBaseGodot {
    /// <summary>指向持续治疗的领域 Buff 定义。</summary>
    protected override BuffDefinition Config => GameConfigDB.BuffHot;
}
