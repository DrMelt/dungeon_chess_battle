namespace DungeonChessBattle.GameConfig.Data;

public class RangeConfig {
}

public class CircularRangeConfig : RangeConfig {
    public float NearClamp { get; set; } = 1.0f;
    public float FarClamp { get; set; } = 1.0f;
    public float RadianFrom { get; set; } = -1.0f;
    public float RadianTo { get; set; } = 1.0f;
}

public class RectRangeConfig : RangeConfig {
    public float NearClamp {
        get; set;
    }
    public float FarClamp { get; set; } = 1.0f;
    public float FromL { get; set; } = -1.0f;
    public float ToR { get; set; } = 1.0f;
}
