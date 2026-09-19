using ErrorOr;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗结算的可预期失败：伤害类型未登记。
/// 描述面向日志，自带定位所需的类型值，由效果实现转交调用方。
/// </summary>
public static class CombatErrors {
    /// <summary>伤害类型未登记。</summary>
    public static Error UnknownDamageType(DamageType type) => Error.Validation(
        code: "Combat.DamageType.Unknown", description: $"伤害类型未登记：{type}");
}
