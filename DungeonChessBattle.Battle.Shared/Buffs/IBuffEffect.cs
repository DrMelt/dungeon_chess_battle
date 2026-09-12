using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>
/// Buff 的持续效果策略：内容侧实现，自持数值配置，无可变状态。
/// 效果只读运行时实例，数值配置不放在定义上。
/// </summary>
public interface IBuffEffect {
    /// <summary>按本次结算节拍时长执行一次效果，返回产生的领域事件，可能为空。</summary>
    IEnumerable<IBattleEvent> Tick(double elapsedSeconds, BuffInstance instance, UnitSnapshot target);
}
