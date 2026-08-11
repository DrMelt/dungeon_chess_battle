namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// Buff 的扁平化展示视图（纯数据）。由编排层把运行时 Buff 实例投影为该结构，
/// 供网络载体（UnitPawn）映射为同步数据渲染。
/// </summary>
public readonly record struct BuffView {
    /// <summary>Buff 类型 ID。</summary>
    public required ushort BuffTypeId {
        get; init;
    }

    /// <summary>剩余持续时间（秒）。</summary>
    public required float Remaining {
        get; init;
    }

    /// <summary>当前叠加层数。</summary>
    public required ushort StackCount {
        get; init;
    }

    /// <summary>伤害类型（仅 DOT 有效，对应 DamageType 的 byte 值）。</summary>
    public required byte DamageType {
        get; init;
    }
}
