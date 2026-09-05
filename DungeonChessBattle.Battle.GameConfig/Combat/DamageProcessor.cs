using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.GameConfig.Combat;

/// <summary>
/// 无状态伤害结算公式：原始 = 基础伤害 × 攻击方攻击系数，结算 = 原始 × 受击方承受系数，
/// 受击方生命钳到 0 与其生命上限之间。
/// </summary>
public static class DamageProcessor {
    /// <summary>结算一次命中：计算攻击加成后的原始伤害，再按受击方承受系数折算并钳制生命。</summary>
    public static DamageResult Process(UnitSnapshot attacker, UnitSnapshot defender, float baseDamage, DamageType type) {
        float raw = type switch {
            DamageType.Physical => baseDamage * attacker.PhysicalAttackBase,
            DamageType.Magic => baseDamage * attacker.MagicAttackBase,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type."),
        };
        float applied = type switch {
            DamageType.Physical => raw * defender.PhysicalTakePercent,
            DamageType.Magic => raw * defender.MagicTakePercent,
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown damage type."),
        };

        float newHealth = System.Math.Clamp(defender.Health - applied, 0f, defender.MaxHealth);
        return new DamageResult {
            RawDamage = raw,
            AppliedDamage = applied,
            ActualHealthLost = defender.Health - newHealth,
            RemainingHealth = newHealth,
        };
    }
}
