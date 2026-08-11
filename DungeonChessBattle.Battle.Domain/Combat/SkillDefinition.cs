using DungeonChessBattle.Battle.Domain.Range;

namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>技能可释放目标类型的标志位。</summary>
[Flags]
public enum SkillTargetPolicy {
    /// <summary>不可主动选择目标释放</summary>
    None = 0,
    /// <summary>可对同阵营单位释放。</summary>
    Same = 1,
    /// <summary>可对敌阵营单位释放。</summary>
    Different = 2,
}

/// <summary>
/// 技能只读定义，纯数据无运行时状态。由配置层 ISkillRepository 提供，
/// 领域结算直接消费数值，不再生成可变运行时模型。
/// </summary>
public abstract class SkillDefinition {
    /// <summary>技能全局唯一 ID。</summary>
    public required ushort SkillId {
        get; init;
    }

    /// <summary>读条时间，秒。</summary>
    public required float SpellTime {
        get; init;
    }

    /// <summary>个体冷却时间，秒。</summary>
    public required float CooldownTime {
        get; init;
    }

    /// <summary>释放成功后触发的全局冷却，秒。</summary>
    public required float GcdTime {
        get; init;
    }

    /// <summary>是否需要锁定单位目标。</summary>
    public required bool NeedUnitTarget {
        get; init;
    }

    /// <summary>是否需要指定位置目标。</summary>
    public required bool NeedPosTarget {
        get; init;
    }

    /// <summary>可释放的目标类型标志。</summary>
    public required SkillTargetPolicy TargetPolicy {
        get; init;
    }
}

/// <summary>单体伤害技能定义。</summary>
public sealed class DamageSkillDefinition : SkillDefinition {
    /// <summary>伤害基础值，经施法者攻击系数换算。</summary>
    public required float Damage {
        get; init;
    }

    /// <summary>伤害类型。</summary>
    public required DamageType DamageType {
        get; init;
    }
}

/// <summary>治疗技能定义。</summary>
public sealed class HealSkillDefinition : SkillDefinition {
    /// <summary>治疗基础值，经施法者治疗强度换算。</summary>
    public required float CurePotency {
        get; init;
    }
}

/// <summary>范围伤害技能定义。</summary>
public sealed class RangeDamageSkillDefinition : SkillDefinition {
    /// <summary>伤害基础值。</summary>
    public required float Damage {
        get; init;
    }

    /// <summary>伤害类型。</summary>
    public required DamageType DamageType {
        get; init;
    }

    /// <summary>范围判定形状。</summary>
    public required RangeShape Range {
        get; init;
    }
}

/// <summary>施加 Buff 的技能定义。</summary>
public sealed class AddBuffSkillDefinition : SkillDefinition {
    /// <summary>释放时施加给目标的 Buff 定义。</summary>
    public required BuffDefinition Buff {
        get; init;
    }
}

/// <summary>
/// Buff 只读定义，纯数据。描述持续效果与叠加规则，由配置层提供。
/// </summary>
public abstract class BuffDefinition {
    /// <summary>Buff 全局唯一 ID。</summary>
    public required ushort BuffTypeId {
        get; init;
    }

    /// <summary>持续时间，秒。</summary>
    public required double Duration {
        get; init;
    }

    /// <summary>最大叠加层数。</summary>
    public required int MaxStacks {
        get; init;
    }
}

/// <summary>持续伤害 DOT Buff 定义。</summary>
public sealed class DamageOverTimeBuff : BuffDefinition {
    /// <summary>每秒伤害基础值。</summary>
    public required float DamagePerSec {
        get; init;
    }

    /// <summary>伤害类型。</summary>
    public required DamageType DamageType {
        get; init;
    }
}

/// <summary>持续治疗 HOT Buff 定义。</summary>
public sealed class HealOverTimeBuff : BuffDefinition {
    /// <summary>每秒治疗基础值。</summary>
    public required float HealthPerSec {
        get; init;
    }
}
