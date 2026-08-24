using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 治疗技能。
/// </summary>
[GlobalClass]
public partial class Skill_Cure : UnitSkillBaseGodot {
    /// <summary>指向治疗的领域技能定义。</summary>
    protected override SkillDefinition Config => GameConfigDB.SkillCure;
}
