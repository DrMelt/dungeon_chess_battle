using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.GameConfig.Combat;

namespace DungeonChessBattle.Battle.GameConfig.Buffs;

/// <summary>持续伤害 DOT 效果。</summary>
public sealed class DotEffect : IBuffEffect {
    /// <inheritdoc />
    public IEnumerable<IBattleEvent> Tick(BuffDefinition definition, double accumulatedSeconds, BuffInstance instance, UnitSnapshot target) {
        if (instance.From is not { } from)
            yield break;

        var dot = (DamageOverTimeBuff)definition;
        float baseDps = dot.DamagePerSec * (float)accumulatedSeconds;
        var result = DamageProcessor.Process(from, target, baseDps, dot.DamageType);
        yield return new DamageOccurred(instance.SourceUnitId, instance.TargetUnitId, result.AppliedDamage, dot.DamageType);
    }
}
