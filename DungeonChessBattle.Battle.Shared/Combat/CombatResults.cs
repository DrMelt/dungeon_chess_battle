namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>一次伤害结算的结果。</summary>
public readonly record struct DamageResult {
    /// <summary>攻击系数加成后的原始伤害。</summary>
    public required float RawDamage {
        get; init;
    }

    /// <summary>经承受系数减免后的结算伤害。</summary>
    public required float AppliedDamage {
        get; init;
    }

    /// <summary>实际扣除的生命值，受当前生命值钳制。</summary>
    public required float ActualHealthLost {
        get; init;
    }

    /// <summary>结算后的剩余生命值。</summary>
    public required float RemainingHealth {
        get; init;
    }
}

/// <summary>一次治疗结算的结果。</summary>
public readonly record struct HealResult {
    /// <summary>治疗系数加成后的原始治疗量。</summary>
    public required float RawHeal {
        get; init;
    }

    /// <summary>实际恢复的生命值，受生命上限钳制。</summary>
    public required float ActualHeal {
        get; init;
    }

    /// <summary>结算后的剩余生命值。</summary>
    public required float RemainingHealth {
        get; init;
    }
}
