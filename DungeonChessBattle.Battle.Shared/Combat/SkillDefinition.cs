using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Range;

namespace DungeonChessBattle.Battle.Shared.Combat;

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
/// 技能只读定义，纯数据无运行时状态。由配置层 GameConfig 提供并装配到战斗单位。
/// 效果策略经 <see cref="Effect"/> 引用注入，规则实现归属内容层。
/// </summary>
public abstract class SkillDefinition {
    /// <summary>技能全局唯一强类型 ID。</summary>
    public required SkillKeyId SkillId {
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

    /// <summary>技能全局冷却配置，必须显式设置；null 表示不参与全局冷却。</summary>
    public required GcdDefinition? Gcd {
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

    /// <summary>
    /// 单位目标技能的最大施法距离，施法者中心到目标中心的距离上限。
    /// 0 表示不设射程限制，兼容未配置射程的旧技能定义。
    /// </summary>
    public float CastRange {
        get; init;
    } = 0f;

    /// <summary>位置目标技能的有效范围形状，非位置目标技能为空。</summary>
    public RangeShape? CastArea {
        get; init;
    }

    /// <summary>释放时执行的效果策略，由内容层构造注入。</summary>
    public required ISkillEffect Effect {
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

/// <summary>仇恨技能定义。</summary>
public sealed class HateSkillDefinition : SkillDefinition {
    /// <summary>仇恨操作类型。</summary>
    public required HateEffectOp Op {
        get; init;
    }

    /// <summary>仇恨操作数值。</summary>
    public required float Value {
        get; init;
    }
}

/// <summary>范围伤害技能定义。有效范围经 <see cref="SkillDefinition.CastArea"/> 表达。</summary>
public sealed class RangeDamageSkillDefinition : SkillDefinition {
    /// <summary>伤害基础值。</summary>
    public required float Damage {
        get; init;
    }

    /// <summary>伤害类型。</summary>
    public required DamageType DamageType {
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
