using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Logic.Combat;

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

/// <summary>
/// 无状态伤害结算规则。忠实复刻既有公式：
/// 原始 = 基础伤害 × 攻击方攻击系数；结算 = 原始 × 受击方承受系数；再钳制到生命上限与下限。
/// </summary>
public static class DamageProcessor {
    /// <summary>
    /// 计算攻击方在给定基础伤害上的实际伤害数值，攻击系数换算。
    /// </summary>
    public static float Amount(float baseDamage, UnitSnapshot attacker, DamageType type) => type switch {
        DamageType.Physical => baseDamage * attacker.PhysicalAttackBase,
        DamageType.Magic => baseDamage * attacker.MagicAttackBase,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type."),
    };

    /// <summary>
    /// 结算一次命中：计算攻击加成后的原始伤害，再按受击方承受系数折算并钳制生命。
    /// </summary>
    public static DamageResult Process(UnitSnapshot attacker, UnitSnapshot defender, float baseDamage, DamageType type) {
        float raw = Amount(baseDamage, attacker, type);
        float applied = type switch {
            DamageType.Physical => raw * defender.PhysicalTakePercent,
            DamageType.Magic => raw * defender.MagicTakePercent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type."),
        };

        float newHealth = Math.Clamp(defender.Health - applied, 0f, defender.MaxHealth);
        return new DamageResult {
            RawDamage = raw,
            AppliedDamage = applied,
            ActualHealthLost = defender.Health - newHealth,
            RemainingHealth = newHealth,
        };
    }
}
