using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_Add_Dot_Magic : UnitSkillBaseGodot {
    protected override SkillConfig Config => GameConfigDB.SkillAddDotMagic;
}
