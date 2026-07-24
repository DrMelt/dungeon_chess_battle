using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Damage : UnitSkillBaseGodot {
    [Export]
    float damage = 0;
    [Export]
    Enum_DamageType enum_DamageType;

    protected override SkillModel CreateModel() {
        return new SkillDamageModel {
            Damage = damage,
            DamageType = enum_DamageType,
        };
    }
}
