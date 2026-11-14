using DungeonChessBattle.Battle.Shared.Combat;
using ErrorOr;

namespace DungeonChessBattle.Battle.Runtime.Shared.Combat.Hates;

/// <summary>
/// 仇恨表写入的可预期失败：仇恨效果的操作类型未登记。
/// 描述面向日志，自带定位所需的操作值。
/// </summary>
public static class HateErrors {
    /// <summary>仇恨效果的操作类型未登记。</summary>
    public static Error UnknownEffectOp(HateEffectOp op) => Error.Validation(
        code: "Hate.EffectOp.Unknown", description: $"仇恨效果操作类型未登记：{op}");
}
