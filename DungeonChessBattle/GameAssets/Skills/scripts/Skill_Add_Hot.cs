using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_Add_Hot : UnitSkillBaseGodot {
    protected override SkillConfig Config => GameConfigDB.SkillAddHot;
}
