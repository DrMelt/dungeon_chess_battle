using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>空 Buff 效果：在线端下行还原 Buff 时绑定，客户端不推进 Buff，效果永不触发。</summary>
public sealed class NoOpBuffEffect : IBuffEffect {
    /// <summary>共享单例。</summary>
    public static readonly NoOpBuffEffect Instance = new();

    /// <inheritdoc />
    public IEnumerable<IBattleEvent> Tick(
        double elapsedSeconds, BuffInstance instance, UnitSnapshot target) =>
        [];
}
