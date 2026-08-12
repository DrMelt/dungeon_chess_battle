using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 魔法持续伤害（DOT）Buff。
/// </summary>
[GlobalClass]
public partial class Buff_DOT : BuffBaseGodot {
    /// <summary>指向魔法 DOT 的 BuffConfig 配置。</summary>
    protected override BuffConfig Config => GameConfigDB.BuffDotMagic;
}
