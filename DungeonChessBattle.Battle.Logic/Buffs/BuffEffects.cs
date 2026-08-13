using DungeonChessBattle.Battle.Domain.Buffs;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Logic.Combat;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>持续伤害 DOT 效果。</summary>
public sealed class DotEffect : IBuffEffect {
    /// <summary>每秒伤害基础值。</summary>
    public required float DamagePerSec {
        get; init;
    }

    /// <summary>伤害类型。</summary>
    public required DamageType DamageType {
        get; init;
    }

    /// <inheritdoc />
    public IEnumerable<IDomainEvent> Tick(double accumulatedSeconds, BuffInstance instance, UnitSnapshot target) {
        if (instance.From is not { } from)
            yield break;

        float baseDps = DamagePerSec * (float)accumulatedSeconds;
        var result = DamageProcessor.Process(from, target, baseDps, DamageType);
        yield return new DamageOccurred(instance.TargetNetId, result.AppliedDamage, DamageType);
    }
}

/// <summary>持续治疗 HOT 效果。</summary>
public sealed class HotEffect : IBuffEffect {
    /// <summary>每秒治疗基础值。</summary>
    public required float HealthPerSec {
        get; init;
    }

    /// <inheritdoc />
    public IEnumerable<IDomainEvent> Tick(double accumulatedSeconds, BuffInstance instance, UnitSnapshot target) {
        if (instance.From is not { } from)
            yield break;

        float baseHps = HealthPerSec * (float)accumulatedSeconds;
        var result = HealProcessor.Process(from, target, baseHps);
        yield return new HealOccurred(instance.TargetNetId, result.ActualHeal);
    }
}

/// <summary>
/// 无状态 Buff 推进规则：按全局结算节拍产出效果事件，并递减剩余时间。
/// </summary>
public static class BuffTickProcessor {
    /// <summary>按帧推进一个 Buff 实例，返回本帧领域事件。失效 Buff 的 IsAlive 会被置为 false。</summary>
    public static IReadOnlyList<IDomainEvent> Tick(IBuffEffect effect, BuffInstance instance, UnitSnapshot target, double deltaTime, double tickSeconds) {
        if (!instance.IsAlive)
            return [];

        var events = new List<IDomainEvent>();

        if (tickSeconds > 0)
            events.AddRange(effect.Tick(tickSeconds, instance, target));

        instance.Remaining -= deltaTime;
        if (instance.Remaining < 0 || instance.Stacks <= 0) {
            instance.IsAlive = false;
            events.Add(new BuffExpired(instance.TargetNetId, instance.BuffTypeId));
        }

        return events;
    }
}
