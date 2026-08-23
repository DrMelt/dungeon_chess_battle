using System.Numerics;

namespace DungeonChessBattle.Client.Replay;

/// <summary>
/// 回放单位展示模型：回放引擎投影器的输出，供回放 UI 只读消费。
/// 在线端等效物为 UnitPawn SyncVar，本模型为纯内存数据，不依赖网络。
/// </summary>
public sealed class ReplayUnitView {
    /// <summary>单位网络 ID，与录制时服务端分配一致。</summary>
    public required ushort NetId {
        get; init;
    }

    /// <summary>单位名称，即单位配置键。</summary>
    public required string UnitName {
        get; init;
    }

    /// <summary>单位所属阵营列表。</summary>
    public required IReadOnlyList<string> Camps {
        get; init;
    }

    /// <summary>当前世界位置，XZ 平面。</summary>
    public Vector2 Position {
        get; set;
    }

    /// <summary>当前朝向方向向量，XZ 平面。</summary>
    public Vector2 Direction {
        get; set;
    }

    /// <summary>当前生命值。</summary>
    public float Health {
        get; set;
    }

    /// <summary>最大生命值。</summary>
    public float MaxHealth {
        get; set;
    }

    /// <summary>当前施法技能 ID，0 表示无施法。</summary>
    public ushort SkillCasting {
        get; set;
    }

    /// <summary>是否已死亡。</summary>
    public bool IsDead {
        get; set;
    }

    /// <summary>聚焦目标单位网络 ID，0 表示无聚焦目标。</summary>
    public ushort FocusTargetNetId {
        get; set;
    }
}
