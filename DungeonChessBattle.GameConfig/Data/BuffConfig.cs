using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.GameConfig.Data;

public class BuffConfig {
    public double Duration { get; set; } = 60;
    public int MaxSuperpositions { get; set; } = 1;
}

public class BuffDOTConfig : BuffConfig {
    public Enum_DamageType DamageType { get; set; } = Enum_DamageType.Magic;
    public float DamagePerSec { get; set; } = 10.0f;
}

public class BuffHOTConfig : BuffConfig {
    public float HealthPerSec { get; set; } = 10.0f;
}
