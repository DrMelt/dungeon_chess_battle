using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Shared.Combat.Hates;

/// <summary>
/// 默认仇恨规则：经典威胁模型。
/// 伤害：被打的单位把攻击者记进自己的仇恨表；治疗：被治疗者阵营的敌对存活单位把治疗者记进自己的仇恨表。
/// 伤害与治疗产生的仇恨量以来源单位的仇恨倍率缩放，见 <see cref="IHateActorView.HateFactor"/>。
/// 仇恨指令（嘲讽）：默认按请求原样落账，把目标仇恨抬到当前最高之上或按操作符修改。
/// 无状态单例，可复用为任意单位的基础规则。
/// </summary>
public sealed class DefaultHateRule : IHateRule {
    /// <summary>规则单例。</summary>
    public static readonly DefaultHateRule Instance = new();

    private DefaultHateRule() {
    }

    /// <inheritdoc />
    public IReadOnlyList<HateEffect> Evaluate(IBattleUnitView self, IBattleEvent e, HateContext ctx) {
        return e switch {
            DamageOccurred dmg when dmg.TargetNetId == self.UnitId && !dmg.SourceNetId.IsDefault => Accrue(self, dmg.SourceNetId, dmg.AppliedDamage, ctx.Settings.DamageHateFactor, ctx),
            HealOccurred heal => HealSpread(self, heal, ctx),
            HateRequested req when req.HolderNetId == self.UnitId => [new HateEffect(self.UnitId, req.SourceNetId, req.Op, req.Value)],
            _ => [],
        };
    }

    /// <summary>按伤害量、治疗量乘来源单位仇恨倍率落账，来源缺失或死亡不落账，零负不落账。</summary>
    private static IReadOnlyList<HateEffect> Accrue(IBattleUnitView self, UnitId sourceNetId, float amount,
        float factor, HateContext ctx) {
        if (ctx.UnitOf(sourceNetId) is not { Health: > 0f } source)
            return [];
        float hate = amount * factor * source.HateFactor;
        return hate <= 0f ? [] : [new HateEffect(self.UnitId, sourceNetId, HateEffectOp.Add, hate)];
    }

    /// <summary>治疗扩散：仅当自身是存活且与被治疗者敌对的单位时，对治疗者记仇。仇恨量以治疗来源倍率缩放。</summary>
    private static IReadOnlyList<HateEffect> HealSpread(IBattleUnitView self, HealOccurred heal, HateContext ctx) {
        if (heal.SourceNetId.IsDefault || self.UnitId == heal.SourceNetId || self.IsDead)
            return [];
        if (ctx.UnitOf(heal.TargetNetId) is not { } healTarget)
            return [];
        if (self.Camps.Count == 0 || healTarget.Camps.Count == 0)
            return [];
        if (ctx.Relations(self.Camps, healTarget.Camps) != CampRelation.Enemy)
            return [];
        return Accrue(self, heal.SourceNetId, heal.ActualHeal, ctx.Settings.HealHateFactor, ctx);
    }
}
