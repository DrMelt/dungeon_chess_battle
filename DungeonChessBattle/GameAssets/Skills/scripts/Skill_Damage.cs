using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 单体魔法伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_Damage : UnitSkillBaseGodot {
    /// <summary>指向单体魔法伤害的领域技能定义。</summary>
    protected override SkillDefinition Config => GameConfigDB.SkillMagicDamage;
}
