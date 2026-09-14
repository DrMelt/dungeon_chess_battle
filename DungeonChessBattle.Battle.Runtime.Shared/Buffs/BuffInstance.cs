using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Runtime.Shared.Buffs;

/// <summary>运行时 Buff 实例：携带来源单位快照、持续计时与叠加层数。</summary>
public sealed class BuffInstance {
    /// <summary>Buff 键，跨端身份：服务端取自定义，在线端取自下行载荷。</summary>
    public required BuffTypeId BuffTypeId {
        get; init;
    }

    /// <summary>目标单位，事件上报用。</summary>
    public required UnitId TargetUnitId {
        get; init;
    }

    /// <summary>施加该 Buff 的来源单位，None 表示无来源；仇恨归属用。</summary>
    public required UnitId SourceUnitId {
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

    /// <summary>伤害类型，自 Buff 定义抄入，展示着色与内容侧结算都读它；非伤害 Buff 为 None。</summary>
    public DamageType DamageType { get; init; } = DamageType.None;

    /// <summary>是否仍生效。</summary>
    public bool IsAlive { get; set; } = true;
}

