using DungeonChessBattle.Core.Enums;

namespace DungeonChessBattle.Battle.Models;

/// <summary>
/// 范围伤害技能模型：释放时对范围内满足条件的目标造成一次伤害。
/// </summary>
public class SkillRangeDamageModel : SkillModel {
    /// <summary>伤害量基础值（经施法单位攻击系数换算）。</summary>
    public float Damage {
        get; set;
    }

    /// <summary>伤害类型（物理/魔法）。</summary>
    public DamageType DamageType {
        get; set;
    }

    /// <summary>范围判定器，用于筛选处于技能范围内的目标。</summary>
    public IRangeChecker? RangeRes {
        get; set;
    }
}
