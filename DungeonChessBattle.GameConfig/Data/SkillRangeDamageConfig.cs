using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 范围伤害技能配置。
/// </summary>
public class SkillRangeDamageConfig : SkillConfig {
    /// <summary>伤害量基础值（经施法单位攻击系数换算）。</summary>
    public float Damage {
        get; set;
    }

    /// <summary>伤害类型（物理/魔法）。</summary>
    public DamageType DamageType { get; set; } = DamageType.Magic;

    /// <summary>技能范围配置。</summary>
    public RangeConfig Range { get; set; } = new CircularRangeConfig();
}
