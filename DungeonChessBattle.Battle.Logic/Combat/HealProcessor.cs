using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Logic.Combat;

/// <summary>
/// 无状态治疗规则。忠实复刻既有公式：治疗量 = 基础强度 × 施法者治疗强度系数，再钳制到生命上限。
/// </summary>
public static class HealProcessor {
    /// <summary>结算一次治疗：按施法者强度折算并钳制受击方生命上限。</summary>
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
