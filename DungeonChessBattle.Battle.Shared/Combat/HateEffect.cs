namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>仇恨账本的修改操作。</summary>
public enum HateEffectOp : byte {
    /// <summary>在既有仇恨上加量。</summary>
    Add = 0,
    /// <summary>既有仇恨乘倍率。</summary>
    Multiply = 1,
    /// <summary>将仇恨提到当前最高值之上的量，用于嘲讽实现。</summary>
    SetTop = 2,
}

/// <summary>
/// 仇恨修改效果：HolderNetId 的仇恨表中对 SourceNetId 的仇恨执行 Op 操作。
/// 由仇恨技能结算产出，直接作用于单位仇恨表。
/// </summary>
public readonly record struct HateEffect(UnitId HolderNetId, UnitId SourceNetId, HateEffectOp Op, float Value);

/// <summary>仇恨快照投影单元，同步用。</summary>
public readonly record struct HateSnapshot(UnitId TargetNetId, float Value);
