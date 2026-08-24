using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>运行时 Buff 实例：携带来源单位快照、持续计时与叠加层数。</summary>
public sealed class BuffInstance {
    /// <summary>Buff 配置 ID。</summary>
    public required ushort BuffTypeId {
        get; init;
    }

    /// <summary>目标单位网络实体 ID，事件上报用。</summary>
    public required ushort TargetNetId {
        get; init;
    }

    /// <summary>施加该 Buff 的来源单位网络 ID，0 表示无来源；仇恨归属用。</summary>
    public required ushort FromNetId {
        get; init;
    }

    /// <summary>施加该 Buff 的来源单位快照；可能为 null。</summary>
    public UnitSnapshot? From {
        get; set;
    }

    /// <summary>剩余持续时间，秒。</summary>
    public double Remaining {
        get; set;
    }

    /// <summary>当前叠加层数。</summary>
    public int Stacks { get; set; } = 1;

    /// <summary>最大可叠加层数。</summary>
    public int MaxStacks { get; init; } = 1;

    /// <summary>伤害类型，供投影展示与 DPS 着色；非伤害 Buff 为 None。</summary>
    public DamageType DamageType { get; init; } = DamageType.None;

    /// <summary>是否仍生效。</summary>
    public bool IsAlive { get; set; } = true;
}

/// <summary>Buff 的持续效果策略，纯函数无状态。</summary>
public interface IBuffEffect {
    /// <summary>按累积秒数执行一次效果，返回产生的领域事件，可能为空。</summary>
    IEnumerable<IBattleEvent> Tick(BuffDefinition definition, double accumulatedSeconds, BuffInstance instance, UnitSnapshot target);
}
