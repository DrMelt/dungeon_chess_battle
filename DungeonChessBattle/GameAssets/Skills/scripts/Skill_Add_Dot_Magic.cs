using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 附加魔法持续伤害（DOT）的技能。
/// </summary>
[GlobalClass]
public partial class Skill_Add_Dot_Magic : UnitSkillBaseGodot {
    /// <summary>指向附加魔法 DOT 的 SkillConfig 配置。</summary>
    protected override SkillConfig Config => GameConfigDB.SkillAddDotMagic;
}
