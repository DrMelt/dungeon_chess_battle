using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Runtime.Shared.Buffs;

/// <summary>
/// 运行时 Buff：实例与其效果策略的绑定，服务端权威状态。
/// 配对不携带定义，效果与展示都读不到内容规则；服务端绑内容效果，客户端无内容故绑空效果。
/// </summary>
public sealed record ActiveBuff(BuffInstance Instance, IBuffEffect Effect) : IBuffView {
    /// <inheritdoc />
    public BuffTypeId BuffTypeId => Instance.BuffTypeId;

    /// <inheritdoc />
    public UnitId TargetUnitId => Instance.TargetUnitId;

    /// <inheritdoc />
    public UnitId SourceUnitId => Instance.SourceUnitId;

    /// <inheritdoc />
    public UnitSnapshot? From => Instance.From;

    /// <inheritdoc />
    public int Stacks => Instance.Stacks;

    /// <inheritdoc />
    public double Remaining => Instance.Remaining;

    /// <inheritdoc />
    public DamageType DamageType => Instance.DamageType;
}
