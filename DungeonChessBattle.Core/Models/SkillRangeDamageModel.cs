using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class SkillRangeDamageModel : SkillModel {
    public float Damage {
        get; set;
    }
    public Enum_DamageType DamageType {
        get; set;
    }
    public IRangeRes? RangeRes {
        get; set;
    }
}
