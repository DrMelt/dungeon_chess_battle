using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic.Combat;

namespace DungeonChessBattle.GameConfig.Buffs;

/// <summary>持续治疗 HOT 效果。</summary>
public sealed class HotEffect : IBuffEffect {
    /// <inheritdoc />
    public IEnumerable<IBattleEvent> Tick(BuffDefinition definition, double accumulatedSeconds, BuffInstance instance, UnitSnapshot target) {
        if (instance.From is not { } from)
            yield break;

        var hot = (HealOverTimeBuff)definition;
        float baseHps = hot.HealthPerSec * (float)accumulatedSeconds;
        var result = HealProcessor.Process(from, target, baseHps);
        yield return new HealOccurred(instance.FromNetId, instance.TargetNetId, result.ActualHeal);
    }
}
