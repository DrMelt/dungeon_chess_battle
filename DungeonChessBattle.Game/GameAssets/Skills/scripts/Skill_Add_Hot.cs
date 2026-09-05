using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 附加持续治疗（HOT）的技能。
/// </summary>
[GlobalClass]
public partial class Skill_Add_Hot : UnitSkillBaseGodot {
    /// <summary>指向附加持续治疗的领域技能定义。</summary>
    protected override SkillDefinition Config => GameConfigDB.SkillAddHot;
}
