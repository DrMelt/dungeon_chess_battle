using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.GameConfig.Combat;

namespace DungeonChessBattle.Battle.GameConfig.Skills;

/// <summary>范围伤害技能效果：遍历战斗世界预过滤的可作用目标，按形状过滤后产伤害事件。</summary>
public sealed class RangeDamageEffect : ISkillEffect {
    /// <inheritdoc />
    public SkillResolution Resolve(SkillResolveContext ctx) {
        var skill = (RangeDamageSkillDefinition)ctx.Skill;
        if (skill.CastArea is not { } area)
            return SkillResolution.Empty;

        var aim = (ctx.TargetPos ?? Vector2.Zero) - ctx.Caster.Snapshot.Position;
        var events = new List<IBattleEvent>();
        foreach (var unit in ctx.Targets) {
            if (!area.Contains(unit.Snapshot.Position, ctx.Caster.Snapshot.Position, aim, unit.Snapshot.BodyRadius))
                continue;
            var result = DamageProcessor.Process(ctx.Caster.Snapshot, unit.Snapshot, skill.Damage, skill.DamageType);
            events.Add(new DamageOccurred(ctx.Caster.UnitId, unit.UnitId, result.AppliedDamage, skill.DamageType));
        }
        return new SkillResolution(events, []);
    }
}
