using ErrorOr;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 战斗事件编码的可预期失败：领域事件类型未登记映射。
/// 描述面向日志，归属与上下文由调用侧补上。
/// </summary>
public static class BattleEventErrors {
    /// <summary>领域事件类型未在 <see cref="BattleEventCoder"/> 登记映射。</summary>
    public static Error UnknownType(string eventTypeName) => Error.Validation(
        code: "BattleEvent.Type.Unknown", description: $"战斗事件类型未登记映射：{eventTypeName}");
}
