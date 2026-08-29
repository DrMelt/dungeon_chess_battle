using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared;

/// <summary>战斗世界同步所需的只读状态契约。状态同步器据此写网络载体，不触碰具体实体；回放端与服务端共用。</summary>
public interface IProjectableBattleState {
    /// <summary>单位网络实体 ID。</summary>
    ushort UnitNetId {
        get;
    }

    /// <summary>当前生命值。</summary>
    float Health {
        get;
    }

    /// <summary>单位是否已死亡：当前生命值 ≤ 0。同步器据此写 UnitState。</summary>
    bool IsDead {
        get;
    }

    /// <summary>最大生命值。</summary>
    float MaxHealth {
        get;
    }

    /// <summary>当前施法技能，default 表示无施法。</summary>
    SkillKeyId SkillCasting {
        get;
    }

    /// <summary>当前施法剩余读条时间，秒。</summary>
    float SkillCastRemaining {
        get;
    }

    /// <summary>全局冷却剩余时间，秒。</summary>
    float GcdRemaining {
        get;
    }

    /// <summary>物理攻击基础系数即伤害倍率。</summary>
    float PhysicalAttackBase {
        get;
    }

    /// <summary>物理伤害承受系数即减免倍率。</summary>
    float PhysicalTakePercent {
        get;
    }

    /// <summary>魔法攻击基础系数即伤害倍率。</summary>
    float MagicAttackBase {
        get;
    }

    /// <summary>魔法伤害承受系数即减免倍率。</summary>
    float MagicTakePercent {
        get;
    }

    /// <summary>治疗强度系数即治疗倍率。</summary>
    float CureIntensity {
        get;
    }

    /// <summary>基础移动速度。</summary>
    float BaseSpeed {
        get;
    }

    /// <summary>碰撞半径。</summary>
    float BodyRadius {
        get;
    }

    /// <summary>当前世界位置，XZ 平面。</summary>
    System.Numerics.Vector2 Position {
        get;
    }

    /// <summary>当前朝向方向向量，XZ 平面。</summary>
    System.Numerics.Vector2 Direction {
        get;
    }

    /// <summary>个体冷却权威列表。</summary>
    IReadOnlyList<CooldownEntry> Cooldowns {
        get;
    }

    /// <summary>当前生效 Buff 权威列表。</summary>
    IReadOnlyList<ActiveBuff> Buffs {
        get;
    }

    /// <summary>当前仇恨快照。</summary>
    IReadOnlyList<HateSnapshot> Hates {
        get;
    }
}
