using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.GameConfig.Combat;

namespace DungeonChessBattle.GameConfig.Skills;

/// <summary>单体治疗技能效果。</summary>
public sealed class HealEffect : ISkillEffect {
    /// <inheritdoc />
    public SkillResolution Resolve(SkillResolveContext ctx) {
        if (ctx.Target is not { } target)
            return SkillResolution.Empty;
        var skill = (HealSkillDefinition)ctx.Skill;
        var result = HealProcessor.Process(ctx.Caster.Snapshot, target.Snapshot, skill.CurePotency);
        return new SkillResolution([new HealOccurred(ctx.Caster.UnitId, target.UnitId, result.ActualHeal)], []);
    }
}
