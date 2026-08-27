using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>
/// 仅用于客户端网络回填占位的 Buff 定义：承载展示字段，效果策略为空，客户端不推进 Buff。
/// 在线端把 UnitPawn 同步的 Buff 还原为 <see cref="ActiveBuff"/> 壳，供 UI 经 <see cref="IBuffUiView"/> 只读取数。
/// </summary>
public sealed class NetworkBuffDefinition : BuffDefinition {
    /// <summary>共享单例：BuffTypeId 等展示值来自 <see cref="BuffInstance"/>，本定义仅作结构占位。</summary>
    public static readonly NetworkBuffDefinition Instance = new() {
        BuffTypeId = 0,
        Duration = 0,
        MaxStacks = 1,
        Effect = NoOpBuffEffect.Instance,
    };

    private NetworkBuffDefinition() {
    }
}

/// <summary>空 Buff 效果：客户端不推进 Buff，效果永不触发。</summary>
public sealed class NoOpBuffEffect : IBuffEffect {
    /// <summary>共享单例。</summary>
    public static readonly NoOpBuffEffect Instance = new();

    /// <inheritdoc />
    public System.Collections.Generic.IEnumerable<IBattleEvent> Tick(
        BuffDefinition definition, double accumulatedSeconds, BuffInstance instance, UnitSnapshot target) =>
        [];
}
