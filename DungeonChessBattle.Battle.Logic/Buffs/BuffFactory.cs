using DungeonChessBattle.Battle.Domain.Buffs;
using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>
/// 把 Buff 只读定义转换为运行时实例与效果策略，纯工厂无状态。
/// 由编排层 BattleRoom 在施加 Buff 时调用。
/// </summary>
public static class BuffFactory {
    /// <summary>根据 Buff 定义创建运行时效果策略。</summary>
    public static IBuffEffect CreateEffect(BuffDefinition def) => def switch {
        DamageOverTimeBuff dot => new DotEffect { DamagePerSec = dot.DamagePerSec, DamageType = dot.DamageType },
        HealOverTimeBuff hot => new HotEffect { HealthPerSec = hot.HealthPerSec },
        _ => throw new ArgumentOutOfRangeException(nameof(def), def.GetType(), "Unknown BuffDefinition type."),
    };

    /// <summary>创建运行时 Buff 实例并绑定效果策略与来源快照。</summary>
    public static BuffInstance CreateInstance(BuffDefinition def, ushort targetNetId, UnitSnapshot? from) => new() {
        BuffTypeId = def.BuffTypeId,
        BuffName = $"buff_{def.BuffTypeId}",
        TargetNetId = targetNetId,
        From = from,
        Remaining = def.Duration,
        MaxStacks = def.MaxStacks,
        Stacks = 1,
    };
}
