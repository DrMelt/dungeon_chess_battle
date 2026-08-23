using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Battle.Domain.Combat.Hates;

/// <summary>
/// 首领仇恨规则：无视一切仇恨指令（含嘲讽），其余行为与默认规则一致。
/// 免疫嘲讽由规则表达而非特判代码，展示单位可按需替换仇恨行为。
/// </summary>
public sealed class BossHateRule : IHateRule {
    /// <summary>规则单例。</summary>
    public static readonly BossHateRule Instance = new();

    private BossHateRule() {
    }

    /// <inheritdoc />
    public IReadOnlyList<HateEffect> Evaluate(IBattleUnitView self, IBattleEvent e, HateContext ctx) {
        if (e is HateRequested)
            return [];
        return DefaultHateRule.Instance.Evaluate(self, e, ctx);
    }
}
