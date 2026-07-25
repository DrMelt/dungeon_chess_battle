namespace DungeonChessBattle.GameConfig.Data;

public class UnitConfig {
    public string Id { get; set; } = "";
    public string UnitStateName { get; set; } = "";
    public float BodyRadius { get; set; } = 1.0f;
    public float MaxHealth { get; set; } = 1000f;
    public float CureIntensity { get; set; } = 1.0f;
    public float PhysicalAttackBase { get; set; } = 1.0f;
    public float PhysicalTakePercent { get; set; } = 1.0f;
    public float MagicAttackBase { get; set; } = 1.0f;
    public float MagicTakePercent { get; set; } = 1.0f;
    public float BaseSpeed { get; set; } = 2.0f;
    public string[] SkillIds { get; set; } = [];
}
