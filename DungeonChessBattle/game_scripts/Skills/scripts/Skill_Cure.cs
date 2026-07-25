using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

[GlobalClass]
public partial class Skill_Cure : UnitSkillBaseGodot {
    protected override SkillConfig Config => GameConfigDB.SkillCure;
}
