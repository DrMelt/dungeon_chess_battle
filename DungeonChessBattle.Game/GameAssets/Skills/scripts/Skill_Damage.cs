using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 单体魔法伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_Damage : UnitSkillBaseGodot {
    /// <summary>指向单体魔法伤害的领域技能定义。</summary>
    protected override SkillDefinition Config =>
        GameContentHost.Registry.GetRequiredSkill(new SkillKeyId(BuiltInContent.SkillKeys.MagicDamage));
}
