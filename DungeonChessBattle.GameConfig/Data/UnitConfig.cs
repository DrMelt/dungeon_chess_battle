namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 单位配置，仅包含策划配表参数，不含运行时状态。
/// </summary>
public class UnitConfig {
    /// <summary>单位碰撞半径。</summary>
    public float BodyRadius { get; set; } = 1.0f;

    /// <summary>最大生命值。</summary>
    public float MaxHealth { get; set; } = 1000f;

    /// <summary>治疗强度系数（治疗量倍率）。</summary>
    public float CureIntensity { get; set; } = 1.0f;

    /// <summary>物理攻击基础系数（伤害倍率）。</summary>
    public float PhysicalAttackBase { get; set; } = 1.0f;

    /// <summary>物理伤害承受系数（减免倍率）。</summary>
    public float PhysicalTakePercent { get; set; } = 1.0f;

    /// <summary>魔法攻击基础系数（伤害倍率）。</summary>
    public float MagicAttackBase { get; set; } = 1.0f;

    /// <summary>魔法伤害承受系数（减免倍率）。</summary>
    public float MagicTakePercent { get; set; } = 1.0f;

    /// <summary>基础移动速度。</summary>
    public float BaseSpeed { get; set; } = 2.0f;

    /// <summary>单位拥有的技能配置列表。</summary>
    public SkillConfig[] Skills { get; set; } = [];
}
