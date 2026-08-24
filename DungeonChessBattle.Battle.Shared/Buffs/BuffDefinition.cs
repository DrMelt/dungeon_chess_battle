using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>
/// Buff 只读定义，纯数据。描述持续效果与叠加规则，由配置层提供。
/// 效果策略经 <see cref="Effect"/> 引用注入，规则实现归属内容层。
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

    /// <summary>伤害类型，供投影展示与事件上报；非伤害 Buff 为 None。</summary>
    public DamageType DamageType {
        get; init;
    } = DamageType.None;

    /// <summary>运行时效果策略，由内容层构造注入。</summary>
    public required IBuffEffect Effect {
        get; init;
    }
}

/// <summary>持续伤害 DOT Buff 定义。</summary>
public sealed class DamageOverTimeBuff : BuffDefinition {
    /// <summary>每秒伤害基础值。</summary>
    public required float DamagePerSec {
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
