using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 单体魔法伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_Damage : UnitSkillBaseGodot {
    /// <summary>指向单体魔法伤害的 SkillConfig 配置。</summary>
    protected override SkillConfig Config => GameConfigDB.SkillMagicDamage;
}
