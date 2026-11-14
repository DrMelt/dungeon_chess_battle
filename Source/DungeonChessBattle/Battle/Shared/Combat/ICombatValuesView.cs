namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗标量与派生数值只读视图：施法校验与展示共用的状态子集。
/// 聚合快照（<see cref="ICombatStatsView.Snapshot"/>）属 <see cref="ICombatStatsView"/>，本接口只暴露判定与展示直接读取的标量。
/// </summary>
public interface ICombatValuesView {
    /// <summary>当前生命值。</summary>
    float Health {
        get;
    }

    /// <summary>单位是否已死亡：当前生命值 ≤ 0。所有消费点统一用本谓词判定，勿再零散比较 Health。</summary>
    bool IsDead {
        get;
    }

    /// <summary>最大生命值，取自配置基础值。</summary>
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
}
