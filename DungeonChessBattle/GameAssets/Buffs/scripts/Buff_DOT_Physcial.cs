using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 物理持续伤害（DOT）Buff。
/// </summary>
[GlobalClass]
public partial class Buff_DOT_Physcial : BuffBaseGodot {
    /// <summary>指向物理 DOT 的 BuffConfig 配置。</summary>
    protected override BuffConfig Config => GameConfigDB.BuffDotPhysical;
}
