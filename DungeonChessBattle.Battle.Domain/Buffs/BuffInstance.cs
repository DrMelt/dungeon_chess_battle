using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Battle.Domain.Buffs;

/// <summary>运行时 Buff 实例：携带目标名、来源单位快照、持续计时与叠加层数。</summary>
public sealed class BuffInstance {
    /// <summary>Buff 配置 ID。</summary>
    public required ushort BuffTypeId {
        get; init;
    }

    /// <summary>Buff 名称，叠加判定的唯一标识。</summary>
    public required string BuffName {
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

    /// <summary>是否仍生效。</summary>
    public bool IsAlive { get; set; } = true;
}

/// <summary>Buff 的持续效果策略，纯函数无状态。</summary>
public interface IBuffEffect {
    /// <summary>按累积秒数执行一次效果，返回产生的领域事件，可能为空。</summary>
    IEnumerable<IDomainEvent> Tick(double accumulatedSeconds, BuffInstance instance, UnitSnapshot target);
}
