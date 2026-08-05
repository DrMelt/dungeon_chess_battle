using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.GameConfig.Data;

/// <summary>
/// 单体伤害技能配置。
/// </summary>
public class SkillDamageConfig : SkillConfig {
    /// <summary>伤害量基础值（经施法单位攻击系数换算）。</summary>
    public float Damage {
        get; set;
    }

    /// <summary>伤害类型（物理/魔法）。</summary>
    public DamageType DamageType { get; set; } = DamageType.Magic;
}
