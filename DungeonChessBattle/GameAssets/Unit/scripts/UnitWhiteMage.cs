using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 白魔法师单位。
/// </summary>
[GlobalClass]
public partial class UnitWhiteMage : UnitState {
    /// <summary>指向白魔法师的 UnitConfig 配置。</summary>
    protected override UnitConfig Config => GameConfigDB.UnitWhiteMage;
}
