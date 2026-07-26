using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.GameConfig.Data;

public class SkillDamageConfig : SkillConfig {
    public float Damage {
        get; set;
    }
    public Enum_DamageType DamageType { get; set; } = Enum_DamageType.Magic;
}
