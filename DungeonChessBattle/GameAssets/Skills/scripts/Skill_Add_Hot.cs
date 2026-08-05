using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 附加持续治疗（HOT）的技能。
/// </summary>
[GlobalClass]
public partial class Skill_Add_Hot : UnitSkillBaseGodot {
    /// <summary>指向附加持续治疗的 SkillConfig 配置。</summary>
    protected override SkillConfig Config => GameConfigDB.SkillAddHot;
}
