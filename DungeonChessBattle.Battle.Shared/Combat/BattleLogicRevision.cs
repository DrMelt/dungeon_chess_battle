namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗结算逻辑修订号，与内容修订号 <c>GameConfigDB.DataRevision</c> 分工并列：
/// 本常量背引擎侧，配置与布局侧由 DataRevision 背。
/// 凡影响重放结果的变更都必须递增——<c>BattleScene.Tick</c> 管线步骤顺序、领域事件产生顺序、
/// 意图消费与作废口径、AI 决策时机、伤害与仇恨结算算法。
/// 录制端写入回放归档，重放端不一致即拒绝重放；纯命名、日志与展示层改动不必递增。
/// </summary>
public static class BattleLogicRevision {
    /// <summary>当前结算逻辑修订号。</summary>
    public const string Value = "1";
}
