using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 持续治疗（HOT）Buff。
/// </summary>
[GlobalClass]
public partial class Buff_HOT : BuffBaseGodot {
    /// <summary>指向持续治疗的 BuffConfig 配置。</summary>
    protected override BuffConfig Config => GameConfigDB.BuffHot;
}
