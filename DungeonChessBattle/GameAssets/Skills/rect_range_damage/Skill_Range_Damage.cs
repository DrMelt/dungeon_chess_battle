using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 矩形范围伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_Range_Damage : UnitSkillBaseGodot {
    /// <summary>指向矩形范围伤害的 SkillConfig 配置。</summary>
    protected override SkillConfig Config => GameConfigDB.SkillRectRangeDamage;
}
