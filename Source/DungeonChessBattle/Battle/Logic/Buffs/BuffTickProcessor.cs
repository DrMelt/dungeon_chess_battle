using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Runtime.Shared.Buffs;
using ErrorOr;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>
/// 无状态 Buff 推进规则：按全局结算节拍求效果事件，并递减剩余时间。
/// 效果结算失败随结果交回调用方，时间推进与失效判定照常，到期事件照常产出。
/// </summary>
public static class BuffTickProcessor {
    /// <summary>按帧推进一个 Buff：返回本帧领域事件与效果错误。失效 Buff 的 IsAlive 会被置为 false。</summary>
    public static BuffTickResult Tick(ActiveBuff buff, UnitSnapshot target, double deltaTime, double tickSeconds) {
        BuffInstance instance = buff.Instance;
        if (!instance.IsAlive)
            return new BuffTickResult([], null);

        Error? effectError = null;
        IReadOnlyList<IBattleEvent> effectEvents = [];
        if (tickSeconds > 0) {
            var effect = buff.Effect.Tick(tickSeconds, buff, target);
            if (effect.IsError)
                effectError = effect.FirstError;
            else
                effectEvents = effect.Value;
        }

        instance.Remaining -= deltaTime;
        bool expired = instance.Remaining <= 0;
        if (expired)
            instance.IsAlive = false;

        var events = new List<IBattleEvent>(effectEvents);
        if (expired)
            events.Add(new BuffExpired(instance.TargetUnitId, instance.BuffTypeId));
        return new BuffTickResult(events, effectError);
    }
}

/// <summary>
/// 单跳结算结果：本跳产出的领域事件与效果错误。
/// 效果错误只否定本跳效果事件，不否定时间推进与到期事件。
/// </summary>
/// <param name="Events">本跳产出的领域事件，含到期事件。</param>
/// <param name="EffectError">效果结算失败原因；成功为空。</param>
public readonly record struct BuffTickResult(IReadOnlyList<IBattleEvent> Events, Error? EffectError);
