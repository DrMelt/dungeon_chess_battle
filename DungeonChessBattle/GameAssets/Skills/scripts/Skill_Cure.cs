using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 治疗技能。
/// </summary>
[GlobalClass]
public partial class Skill_Cure : UnitSkillBaseGodot {
    /// <summary>指向治疗的 SkillConfig 配置。</summary>
    protected override SkillConfig Config => GameConfigDB.SkillCure;
}
