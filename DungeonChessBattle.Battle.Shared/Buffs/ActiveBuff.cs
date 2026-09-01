using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>运行时 Buff：定义、实例与效果策略的配对，服务端权威状态。</summary>
public sealed record ActiveBuff(BuffInstance Instance, BuffDefinition Definition, IBuffEffect Effect) : IBuffUiView {
    /// <inheritdoc />
    public ushort BuffTypeId => Instance.BuffTypeId;

    /// <inheritdoc />
    public int Stacks => Instance.Stacks;

    /// <inheritdoc />
    public int MaxStacks => Instance.MaxStacks;

    /// <inheritdoc />
    public double Remaining => Instance.Remaining;

    /// <inheritdoc />
    public UnitId SourceUnitId => Instance.SourceUnitId;

    /// <inheritdoc />
    public DamageType DamageType => Instance.DamageType;
}
