using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 圆形范围伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_CircularRange_Damage : UnitSkillBaseGodot {
    // TODO: GameConfigDB 中暂无对应的圆形范围伤害配置，暂时使用矩形范围伤害配置
    /// <summary>指向实际使用的矩形范围伤害 SkillConfig 配置。</summary>
    protected override SkillConfig Config => GameConfigDB.SkillRectRangeDamage;
}
