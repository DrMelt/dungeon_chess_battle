using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 附加魔法持续伤害（DOT）的技能。
/// </summary>
[GlobalClass]
public partial class Skill_Add_Dot_Magic : UnitSkillBaseGodot {
    /// <summary>指向附加魔法 DOT 的领域技能定义。</summary>
    protected override SkillDefinition Config => GameConfigDB.SkillAddDotMagic;
}
