using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Range;

namespace DungeonChessBattle.Battle.Config.Shared.Combat;

/// <summary>
/// 技能只读定义：引擎消费的身份、施法规则与目标规则，加注内容侧行为引用。不可继承，无运行时状态。
/// 引擎内部按定义取规则；内容侧只按技能键持有引用。
/// 各技能的效果数值由 <see cref="ISkillEffect"/> 实现自持；范围效果的形状经 <see cref="CastArea"/> 声明，效果只读取用。
/// </summary>
public sealed class SkillDefinition {
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
    /// null 表示不设射程限制，必须显式声明取哪一种。
    /// </summary>
    public required float? CastRange {
        get; init;
    }

    /// <summary>位置目标技能的有效范围形状，非位置目标技能为空。</summary>
    public IRangeShape? CastArea {
        get; init;
    }

    /// <summary>释放时执行的效果策略，由内容层构造注入。</summary>
    public required ISkillEffect Effect {
        get; init;
    }
}

