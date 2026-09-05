using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.GameConfig.Skills;

/// <summary>单体仇恨技能效果。</summary>
public sealed class HateSkillEffect : ISkillEffect {
    /// <inheritdoc />
    public SkillResolution Resolve(SkillResolveContext ctx) {
        if (ctx.Target is not { } target)
            return SkillResolution.Empty;
        var skill = (HateSkillDefinition)ctx.Skill;
        return new SkillResolution([new HateRequested(target.UnitId, ctx.Caster.UnitId, skill.Op, skill.Value)], []);
    }
}
