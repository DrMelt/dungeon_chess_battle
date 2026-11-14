using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using ErrorOr;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>
/// Buff 的持续效果策略：内容侧实现，自持数值配置，无可变状态。
/// 效果只读运行时实例，数值配置不放在定义上。
/// 数值非法等可预期失败以错误返回，由战斗世界记一条日志并只丢本跳效果事件。
/// </summary>
public interface IBuffEffect {
    /// <summary>按本次结算节拍时长执行一次效果，返回产生的领域事件，可能为空。</summary>
    ErrorOr<IReadOnlyList<IBattleEvent>> Tick(double elapsedSeconds, IBuffView instance, UnitSnapshot target);
}
