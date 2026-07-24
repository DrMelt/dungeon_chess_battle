using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Range;
using Godot;

namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_CircularRange_Damage : UnitSkillBaseGodot {
    [Export]
    float damage = 0;
    [Export]
    Enum_DamageType enum_DamageType;

    [Export]
    float nearClamp = 1.0f;
    [Export]
    float farClamp = 1.0f;
    [Export]
    float radianFrom = -1.0f;
    [Export]
    float radianTo = 1.0f;

    protected override SkillModel CreateModel() {
        return new SkillRangeDamageModel {
            Damage = damage,
            DamageType = enum_DamageType,
            RangeRes = new CircularRangeRes {
                NearClamp = nearClamp,
                FarClamp = farClamp,
                RadianFrom = radianFrom,
                RadianTo = radianTo,
            },
        };
    }
}
