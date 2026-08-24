using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic.Combat;

namespace DungeonChessBattle.GameConfig.Skills;

/// <summary>单体伤害技能效果。</summary>
public sealed class DamageEffect : ISkillEffect {
    /// <inheritdoc />
    public SkillResolution Resolve(SkillResolveContext ctx) {
        if (ctx.Target is not { } target)
            return SkillResolution.Empty;
        var skill = (DamageSkillDefinition)ctx.Skill;
        var result = DamageProcessor.Process(ctx.Caster.Snapshot, target.Snapshot, skill.Damage, skill.DamageType);
        return new SkillResolution([new DamageOccurred(ctx.Caster.UnitNetId, target.UnitNetId, result.AppliedDamage, skill.DamageType)], []);
    }
}
