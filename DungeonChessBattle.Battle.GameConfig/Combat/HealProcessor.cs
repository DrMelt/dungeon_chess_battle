using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.GameConfig.Combat;

/// <summary>
/// 无状态治疗结算公式：治疗量 = 基础强度 × 施法者治疗强度系数，
/// 被治疗方生命钳到 0 与其生命上限之间。
/// </summary>
public static class HealProcessor {
    /// <summary>结算一次治疗：按施法者治疗强度系数折算，再钳制被治疗方生命。</summary>
    public static HealResult Process(UnitSnapshot healer, UnitSnapshot target, float basePotency) {
        float raw = basePotency * healer.CureIntensity;
        float newHealth = System.Math.Clamp(target.Health + raw, 0f, target.MaxHealth);
        return new HealResult {
            RawHeal = raw,
            ActualHeal = newHealth - target.Health,
            RemainingHealth = newHealth,
        };
    }
}
