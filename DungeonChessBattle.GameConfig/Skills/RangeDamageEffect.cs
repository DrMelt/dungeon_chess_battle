using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Logic.Combat;

namespace DungeonChessBattle.GameConfig.Skills;

/// <summary>范围伤害技能效果：遍历候选单位，按阵营与范围过滤后产伤害事件。</summary>
public sealed class RangeDamageEffect : ISkillEffect {
    /// <inheritdoc />
    public SkillResolution Resolve(SkillResolveContext ctx) {
        var skill = (RangeDamageSkillDefinition)ctx.Skill;
        if (skill.CastArea is not { } area)
            return SkillResolution.Empty;

        var aim = (ctx.TargetPos ?? Vector2.Zero) - ctx.Caster.Snapshot.Position;
        var events = new List<IBattleEvent>();
        foreach (var unit in ctx.Candidates) {
            if (unit.UnitNetId == ctx.Caster.UnitNetId)
                continue;
            if (!SkillTargetValidator.CanAffect(ctx.Caster, unit, skill.TargetPolicy, ctx.Relations))
                continue;
            if (!area.Contains(unit.Snapshot.Position, ctx.Caster.Snapshot.Position, aim, unit.Snapshot.BodyRadius))
                continue;
            var result = DamageProcessor.Process(ctx.Caster.Snapshot, unit.Snapshot, skill.Damage, skill.DamageType);
            events.Add(new DamageOccurred(ctx.Caster.UnitNetId, unit.UnitNetId, result.AppliedDamage, skill.DamageType));
        }
        return new SkillResolution(events, []);
    }
}
