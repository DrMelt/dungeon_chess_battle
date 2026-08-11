namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 编排层读写具体战斗单位的通道（依赖倒置：由 Entities 的 UnitPawn 实现）。
/// Logic 层面向本接口编排结算，不依赖任何网络/框架类型。
/// 读条、冷却、Buff 为服务端每帧推进的权威状态，通过本接口写回载体同步。
/// </summary>
public interface IBattleUnit {
    /// <summary>单位名称（调试与事件标识用）。</summary>
    string UnitName {
        get;
    }

    /// <summary>单位所属阵营列表。</summary>
    IReadOnlyList<string> Camps {
        get;
    }

    /// <summary>当前战斗结算快照（只读输入）。</summary>
    UnitSnapshot Snapshot {
        get;
    }

    /// <summary>当前生命值。</summary>
    float Health {
        get; set;
    }

    /// <summary>最大生命值。</summary>
    float MaxHealth {
        get;
    }

    /// <summary>当前施法技能 ID（0=无施法）。</summary>
    ushort SkillCasting {
        get; set;
    }

    /// <summary>当前施法剩余读条时间（秒）。</summary>
    float SkillCastRemaining {
        get; set;
    }

    /// <summary>剩余全局冷却时间（秒）。</summary>
    float GcdRemaining {
        get; set;
    }

    /// <summary>技能个体冷却快照（技能 ID → 剩余秒数）。</summary>
    IReadOnlyDictionary<ushort, float> SkillCooldowns {
        get;
    }

    /// <summary>写入单个技能的个体冷却（剩余秒数）。</summary>
    void SetSkillCooldown(ushort skillId, float remaining);

    /// <summary>当前 Buff 快照（展示投影）。</summary>
    IReadOnlyList<BuffView> Buffs {
        get;
    }

    /// <summary>以全量投影方式同步 Buff 列表（服务端权威）。</summary>
    void ReplaceBuffs(IReadOnlyList<BuffView> buffs);
}
