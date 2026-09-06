using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 矩形范围伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_Range_Damage : UnitSkillBaseGodot {
    /// <summary>指向矩形范围伤害的领域技能定义。</summary>
    protected override SkillDefinition Config =>
        GameContentHost.Registry.GetRequiredSkill(new SkillKeyId(BuiltInContent.SkillKeys.RectRangeDamage));
}
