using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Logic.Combat;

/// <summary>一次治疗结算的结果。</summary>
public readonly record struct HealResult {
    /// <summary>治疗系数加成后的原始治疗量。</summary>
    public required float RawHeal {
        get; init;
    }

    /// <summary>实际恢复的生命值（受生命上限钳制）。</summary>
    public required float ActualHeal {
        get; init;
    }

    /// <summary>结算后的剩余生命值。</summary>
    public required float RemainingHealth {
        get; init;
    }
}

/// <summary>
/// 无状态治疗规则。忠实复刻既有公式：治疗量 = 基础强度 × 施法者治疗强度系数，再钳制到生命上限。
/// </summary>
public static class HealProcessor {
    /// <summary>计算施法者在给定基础治疗量上的实际治疗数值（强度换算）。</summary>
    public static float Amount(float basePotency, UnitSnapshot healer) => basePotency * healer.CureIntensity;

    /// <summary>结算一次治疗：按施法者强度折算并钳制受击方生命上限。</summary>
    public static HealResult Process(UnitSnapshot healer, UnitSnapshot target, float basePotency) {
        float raw = Amount(basePotency, healer);
        float newHealth = System.Math.Clamp(target.Health + raw, 0f, target.MaxHealth);
        return new HealResult {
            RawHeal = raw,
            ActualHeal = newHealth - target.Health,
            RemainingHealth = newHealth,
        };
    }
}
