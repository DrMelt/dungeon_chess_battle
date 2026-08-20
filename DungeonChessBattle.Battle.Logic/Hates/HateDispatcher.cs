using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Battle.Logic.Hates;

/// <summary>
/// 仇恨分发器：把本帧领域事件逐个交给每个单位，由单位自身规则评估产出仇恨效果。
/// 无状态纯函数，只输出效果列表不落账；落账由 BattleScene 按持有者路由到单位仇恨表。
/// 目标对象为中心：只有规则自身认为与事件相关时才产生效果，且只写本单位自己的仇恨表。
/// </summary>
public static class HateDispatcher {
    /// <summary>事件类型到仇恨处理的关注度预筛，与事件无关的类型直接跳过。</summary>
    private static bool IsRelevant(IBattleEvent evt) =>
        evt is DamageOccurred or HealOccurred or HateRequested;

    /// <summary>分发一件事流到全部存活单位求值，返回聚合仇恨效果。单位索引由编排层持有，规则反查共用同一份。</summary>
    public static IReadOnlyList<HateEffect> Dispatch(
        IReadOnlyList<IBattleEvent> events,
        IReadOnlyList<IBattleUnit> units,
        IReadOnlyDictionary<ushort, IBattleUnit> unitById,
        HateSettings settings,
        CampRelationResolver relations) {
        if (events.Count == 0 || units.Count == 0)
            return [];

        var ctx = new HateContext(settings, unitById.GetValueOrDefault, relations);

        var effects = new List<HateEffect>();
        foreach (var evt in events) {
            if (!IsRelevant(evt))
                continue;
            foreach (var unit in units) {
                if (unit.Health <= 0f)
                    continue;
                effects.AddRange(unit.HateRule.Evaluate(unit, evt, ctx));
            }
        }
        return effects;
    }
}
