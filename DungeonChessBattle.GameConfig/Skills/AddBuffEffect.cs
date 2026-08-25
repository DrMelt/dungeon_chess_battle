using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.GameConfig.Skills;

/// <summary>施加 Buff 的技能效果。</summary>
public sealed class AddBuffEffect : ISkillEffect {
    /// <inheritdoc />
    public SkillResolution Resolve(SkillResolveContext ctx) {
        if (ctx.Target is not { } target)
            return SkillResolution.Empty;
        var skill = (AddBuffSkillDefinition)ctx.Skill;
        return new SkillResolution([], [new BuffToApply(skill.Buff, target.UnitNetId, ctx.Caster.Snapshot, ctx.Caster.UnitNetId)]);
    }
}
