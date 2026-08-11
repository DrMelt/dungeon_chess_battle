using System.Numerics;

namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>伤害类型。</summary>
public enum DamageType {
    /// <summary>无伤害。</summary>
    None = 0,
    /// <summary>物理伤害。</summary>
    Physical,
    /// <summary>魔法伤害。</summary>
    Magic,
}

/// <summary>
/// 战斗结算所需的只读单位快照，值类型。
/// 领域规则只消费该快照，不接触网络实体，保证纯函数可独立测试。
/// </summary>
public readonly record struct UnitSnapshot {
    /// <summary>当前生命值。</summary>
    public required float Health {
        get; init;
    }

    /// <summary>最大生命值。</summary>
    public required float MaxHealth {
        get; init;
    }

    /// <summary>物理攻击基础系数即伤害倍率。</summary>
    public required float PhysicalAttackBase {
        get; init;
    }

    /// <summary>物理伤害承受系数即减免倍率。</summary>
    public required float PhysicalTakePercent {
        get; init;
    }

    /// <summary>魔法攻击基础系数即伤害倍率。</summary>
    public required float MagicAttackBase {
        get; init;
    }

    /// <summary>魔法伤害承受系数即减免倍率。</summary>
    public required float MagicTakePercent {
        get; init;
    }

    /// <summary>治疗强度系数即治疗倍率。</summary>
    public required float CureIntensity {
        get; init;
    }

    /// <summary>当前移动速度。</summary>
    public float MoveSpeed {
        get; init;
    }

    /// <summary>世界坐标，XZ 平面，供范围判定使用。</summary>
    public Vector2 Position {
        get; init;
    }

    /// <summary>碰撞半径，供技能范围判定使用。</summary>
    public float BodyRadius {
        get; init;
    }
}
