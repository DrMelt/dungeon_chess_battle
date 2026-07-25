using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_Range_Damage : UnitSkillBaseGodot {
    protected override SkillConfig Config => GameConfigDB.SkillRectRangeDamage;
}
