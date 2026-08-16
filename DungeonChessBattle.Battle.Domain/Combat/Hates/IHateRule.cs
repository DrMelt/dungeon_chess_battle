using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Battle.Domain.Combat.Hates;

/// <summary>
/// 仇恨规则评估上下文：事件求值所需的环境依赖，由编排层装配注入。
/// 规则为纯函数，不得引用房间内部状态。
/// </summary>
/// <param name="Settings">仇恨系统参数。</param>
/// <param name="UnitOf">按网络 ID 反查单位；已移除单位返回 null。</param>
/// <param name="Relations">阵营关系函数。</param>
public readonly record struct HateContext(
    HateSettings Settings,
    Func<ushort, IBattleUnit?> UnitOf,
    CampRelationResolver Relations);

/// <summary>
/// 单位仇恨规则：以自身为中心评估领域事件，返回要落账的仇恨效果。
/// 不变量：效果持有者恒为本单位网络 ID，规则只允许写入自己的仇恨表。
/// Evaluate 必须为纯函数，只读仇恨数与上下文，不修改外部状态。
/// </summary>
public interface IHateRule {
    /// <summary>以自身为中心求值一个领域事件，返回落账效果；事件与自身无关时返回空。</summary>
    IReadOnlyList<HateEffect> Evaluate(IBattleUnit self, IDomainEvent e, HateContext ctx);
}
