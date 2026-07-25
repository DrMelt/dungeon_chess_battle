namespace DungeonChessBattle.GameConfig.Data;

public class SkillRangeDamageConfig : SkillConfig {
    public float Damage {
        get; set;
    }
    public string DamageType { get; set; } = "Magic";
    public RangeConfig Range { get; set; } = new CircularRangeConfig();
}
