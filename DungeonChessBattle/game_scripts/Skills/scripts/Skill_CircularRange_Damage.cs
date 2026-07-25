using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_CircularRange_Damage : UnitSkillBaseGodot {
    // TODO: GameConfigDB 中暂无对应的圆形范围伤害配置，暂时使用矩形范围伤害配置
    protected override SkillConfig Config => GameConfigDB.SkillRectRangeDamage;
}
