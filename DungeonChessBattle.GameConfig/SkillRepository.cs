using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Range;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 领域技能仓库 ISkillRepository 的配置层实现：把只读 SkillConfig 映射为
/// Domain 的 SkillDefinition 供 BattleRoom 结算。映射为纯转换，无运行时状态。
/// </summary>
public sealed class SkillRepository : ISkillRepository {
    /// <inheritdoc />
    public SkillDefinition? Get(ushort skillId) {
        var config = GameConfigDB.GetSkillById(skillId);
        return config == null ? null : Map(config);
    }

    private static SkillDefinition Map(SkillConfig c) {
        return c switch {
            SkillDamageConfig d => new DamageSkillDefinition {
                SkillId = c.Id, SpellTime = c.SkillSpellTime, CooldownTime = c.SkillCooldownTime,
                GcdTime = c.GCDTime, NeedUnitTarget = c.NeedUnitTarget, NeedPosTarget = c.NeedPosTarget,
                TargetPolicy = MapPolicy(c.SkillCanAdd),
                Damage = d.Damage, DamageType = d.DamageType,
            },
            SkillCureConfig cure => new HealSkillDefinition {
                SkillId = c.Id, SpellTime = c.SkillSpellTime, CooldownTime = c.SkillCooldownTime,
                GcdTime = c.GCDTime, NeedUnitTarget = c.NeedUnitTarget, NeedPosTarget = c.NeedPosTarget,
                TargetPolicy = MapPolicy(c.SkillCanAdd),
                CurePotency = cure.CurePotency,
            },
            SkillAddBuffConfig addBuff => new AddBuffSkillDefinition {
                SkillId = c.Id, SpellTime = c.SkillSpellTime, CooldownTime = c.SkillCooldownTime,
                GcdTime = c.GCDTime, NeedUnitTarget = c.NeedUnitTarget, NeedPosTarget = c.NeedPosTarget,
                TargetPolicy = MapPolicy(c.SkillCanAdd),
                Buff = MapBuff(addBuff.BuffConfig),
            },
            SkillRangeDamageConfig range => new RangeDamageSkillDefinition {
                SkillId = c.Id, SpellTime = c.SkillSpellTime, CooldownTime = c.SkillCooldownTime,
                GcdTime = c.GCDTime, NeedUnitTarget = c.NeedUnitTarget, NeedPosTarget = c.NeedPosTarget,
                TargetPolicy = MapPolicy(c.SkillCanAdd),
                Damage = range.Damage, DamageType = range.DamageType,
                Range = MapRange(range.Range),
            },
            _ => throw new InvalidOperationException($"Unknown SkillConfig type: {c.GetType().Name}"),
        };
    }

    private static BuffDefinition MapBuff(BuffConfig b) => b switch {
        BuffDOTConfig dot => new DamageOverTimeBuff {
            BuffTypeId = dot.Id, Duration = dot.Duration, MaxStacks = dot.MaxSuperpositions,
            DamagePerSec = dot.DamagePerSec, DamageType = dot.DamageType,
        },
        BuffHOTConfig hot => new HealOverTimeBuff {
            BuffTypeId = hot.Id, Duration = hot.Duration, MaxStacks = hot.MaxSuperpositions,
            HealthPerSec = hot.HealthPerSec,
        },
        _ => throw new InvalidOperationException($"Unknown BuffConfig type: {b.GetType().Name}"),
    };

    private static RangeShape MapRange(RangeConfig r) => r switch {
        CircularRangeConfig c => new SectorShape {
            NearClamp = c.NearClamp, FarClamp = c.FarClamp,
            RadianFrom = c.RadianFrom, RadianTo = c.RadianTo,
        },
        RectRangeConfig rect => new RectShape {
            NearClamp = rect.NearClamp, FarClamp = rect.FarClamp,
            FromLeft = rect.FromL, ToRight = rect.ToR,
        },
        _ => throw new InvalidOperationException($"Unknown RangeConfig type: {r.GetType().Name}"),
    };

    private static SkillTargetPolicy MapPolicy(string skillCanAdd)
        => Enum.Parse<SkillTargetPolicy>(skillCanAdd, ignoreCase: true);
}
