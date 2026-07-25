namespace DungeonChessBattle.GameConfig.Data;

public class SkillDamageConfig : SkillConfig {
    public float Damage {
        get; set;
    }
    public string DamageType { get; set; } = "Magic";
}
