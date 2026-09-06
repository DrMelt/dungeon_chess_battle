using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 圆形范围伤害技能。
/// </summary>
[GlobalClass]
public partial class Skill_CircularRange_Damage : UnitSkillBaseGodot {
    // TODO: 暂无对应的圆形范围伤害配置，暂时使用矩形范围伤害配置
    /// <summary>指向实际使用的矩形范围伤害领域技能定义。</summary>
    protected override SkillDefinition Config =>
        GameContentHost.Registry.GetRequiredSkill(new SkillKeyId(BuiltInContent.SkillKeys.RectRangeDamage));
}
