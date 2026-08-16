using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.GameConfig;
using Godot;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 单体嘲讽技能：把目标敌人对本单位的仇恨抬到最高之上。
/// </summary>
[GlobalClass]
public partial class Skill_Taunt : UnitSkillBaseGodot {
    /// <summary>指向单体嘲讽的领域技能定义。</summary>
    protected override SkillDefinition Config => GameConfigDB.SkillTaunt;
}
