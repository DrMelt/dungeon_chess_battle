using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>
/// 无状态 Buff 推进规则：按全局结算节拍产出效果事件，并递减剩余时间。
/// </summary>
public static class BuffTickProcessor {
    /// <summary>按帧推进一个 Buff 实例，返回本帧领域事件。失效 Buff 的 IsAlive 会被置为 false。</summary>
    public static IReadOnlyList<IBattleEvent> Tick(BuffDefinition definition, IBuffEffect effect, BuffInstance instance, UnitSnapshot target, double deltaTime, double tickSeconds) {
        if (!instance.IsAlive)
            return [];

        var events = new List<IBattleEvent>();

        if (tickSeconds > 0)
            events.AddRange(effect.Tick(definition, tickSeconds, instance, target));

        instance.Remaining -= deltaTime;
        if (instance.Remaining <= 0) {
            instance.IsAlive = false;
            events.Add(new BuffExpired(instance.TargetUnitId, instance.BuffTypeId));
        }

        return events;
    }
}
