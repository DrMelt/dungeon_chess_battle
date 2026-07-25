namespace DungeonChessBattle.GameConfig.Data;

public class BuffConfig {
    public string Id { get; set; } = "";
    public string BuffName { get; set; } = "";
    public string BuffDescription { get; set; } = "";
    public string IconPath { get; set; } = "";
    public double Duration { get; set; } = 60;
    public int MaxSuperpositions { get; set; } = 1;
}

public class BuffDOTConfig : BuffConfig {
    public string DamageType { get; set; } = "Magic";
    public float DamagePerSec { get; set; } = 10.0f;
}

public class BuffHOTConfig : BuffConfig {
    public float HealthPerSec { get; set; } = 10.0f;
}
