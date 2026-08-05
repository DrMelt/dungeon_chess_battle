namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 技能范围配置基类，用于转换生成范围判定器。
/// </summary>
public class RangeConfig {
}

/// <summary>
/// 扇形（环形）范围配置。
/// </summary>
public class CircularRangeConfig : RangeConfig {
    /// <summary>近端半径。</summary>
    public float NearClamp { get; set; } = 1.0f;

    /// <summary>远端半径。</summary>
    public float FarClamp { get; set; } = 1.0f;

    /// <summary>扇形起始角（弧度）。</summary>
    public float RadianFrom { get; set; } = -1.0f;

    /// <summary>扇形结束角（弧度）。</summary>
    public float RadianTo { get; set; } = 1.0f;
}

/// <summary>
/// 矩形范围配置。
/// </summary>
public class RectRangeConfig : RangeConfig {
    /// <summary>近端沿朝向的边界。</summary>
    public float NearClamp {
        get; set;
    }

    /// <summary>远端沿朝向的边界。</summary>
    public float FarClamp { get; set; } = 1.0f;

    /// <summary>左侧横向边界。</summary>
    public float FromL { get; set; } = -1.0f;

    /// <summary>右侧横向边界。</summary>
    public float ToR { get; set; } = 1.0f;
}
