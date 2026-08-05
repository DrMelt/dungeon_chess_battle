using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Core.Models;

/// <summary>
/// 单体伤害技能模型：释放时对目标造成一次伤害。
/// </summary>
public class SkillDamageModel : SkillModel {
    /// <summary>伤害量基础值（经施法单位攻击系数换算）。</summary>
    public float Damage {
        get; set;
    }

    /// <summary>伤害类型（物理/魔法）。</summary>
    public DamageType DamageType {
        get; set;
    }
}
