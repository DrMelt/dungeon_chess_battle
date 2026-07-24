using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Range_Damage : UnitSkillBaseGodot {
    [Export]
    float damage = 0;

    [Export]
    Enum_DamageType enum_DamageType;

    [Export]
    RangeResBaseGodot range_Res_Base;
    public RangeResBaseGodot Skill_Range_Res_Base => range_Res_Base;

    protected override SkillModel CreateModel() {
        return new SkillRangeDamageModel {
            Damage = damage,
            DamageType = enum_DamageType,
            RangeRes = range_Res_Base,
        };
    }
}
