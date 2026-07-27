using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Models;

public class SkillDamageModel : SkillModel {
    public float Damage {
        get; set;
    }
    public Enum_DamageType DamageType {
        get; set;
    }
}
