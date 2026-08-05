using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// Buff 配置基类，仅包含策划配表参数，不含运行时状态。
/// </summary>
public class BuffConfig {
    /// <summary>持续时间（秒）。</summary>
    public double Duration { get; set; } = 60;

    /// <summary>最大叠加层数。</summary>
    public int MaxSuperpositions { get; set; } = 1;
}

/// <summary>
/// 持续伤害（DOT）Buff 配置。
/// </summary>
public class BuffDOTConfig : BuffConfig {
    /// <summary>伤害类型。</summary>
    public DamageType DamageType { get; set; } = DamageType.Magic;

    /// <summary>每秒造成的伤害量。</summary>
    public float DamagePerSec { get; set; } = 10.0f;
}

/// <summary>
/// 持续治疗（HOT）Buff 配置。
/// </summary>
public class BuffHOTConfig : BuffConfig {
    /// <summary>每秒恢复的生命值。</summary>
    public float HealthPerSec { get; set; } = 10.0f;
}
