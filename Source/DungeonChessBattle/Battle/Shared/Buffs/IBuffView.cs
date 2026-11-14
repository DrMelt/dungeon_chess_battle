using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>
/// Buff 实例只读视图：效果读来源与伤害类型，展示读层数与剩余时间。
/// 由运行时 Buff 载体实现，不暴露写通道。
/// </summary>
public interface IBuffView {
    /// <summary>Buff 键，跨端身份：服务端取自定义，在线端取自下行载荷。</summary>
    BuffTypeId BuffTypeId {
        get;
    }

    /// <summary>目标单位，事件上报用。</summary>
    UnitId TargetUnitId {
        get;
    }

    /// <summary>施加该 Buff 的来源单位，None 表示无来源；仇恨归属用。</summary>
    UnitId SourceUnitId {
        get;
    }

    /// <summary>施加该 Buff 的来源单位快照；可能为 null。</summary>
    UnitSnapshot? From {
        get;
    }

    /// <summary>剩余持续时间，秒。</summary>
    double Remaining {
        get;
    }

    /// <summary>当前叠加层数。</summary>
    int Stacks {
        get;
    }

    /// <summary>伤害类型，展示着色与内容侧结算都读它；非伤害 Buff 为 None。</summary>
    DamageType DamageType {
        get;
    }
}
