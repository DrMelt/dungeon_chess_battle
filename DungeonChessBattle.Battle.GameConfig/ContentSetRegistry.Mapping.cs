using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Range;
using DungeonChessBattle.Battle.Mod;
using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.GameConfig;

public sealed partial class ContentSetRegistry {
    private void BuildSkills(ModContentJson content, BehaviorCatalog catalog) {
        foreach (var dto in content.Skills) {
            SkillDefinition skill = dto.Kind switch {
                "damage" => new DamageSkillDefinition {
                    SkillId = new SkillKeyId(dto.Id),
                    SpellTime = dto.SpellTime,
                    CooldownTime = dto.CooldownTime,
                    Gcd = MapGcd(dto.Gcd),
                    NeedUnitTarget = dto.NeedUnitTarget,
                    NeedPosTarget = dto.NeedPosTarget,
                    TargetPolicy = Parse<SkillTargetPolicy>(dto.TargetPolicy),
                    CastRange = dto.CastRange,
                    Damage = dto.Damage ?? throw MissingField(dto.Id, "damage"),
                    DamageType = Parse<DamageType>(dto.DamageType),
                    Effect = catalog.GetSkillEffect(DtoOrDefault(dto.Effect, BehaviorIds.SkillEffect.Damage)),
                },
                "heal" => new HealSkillDefinition {
                    SkillId = new SkillKeyId(dto.Id),
                    SpellTime = dto.SpellTime,
                    CooldownTime = dto.CooldownTime,
                    Gcd = MapGcd(dto.Gcd),
                    NeedUnitTarget = dto.NeedUnitTarget,
                    NeedPosTarget = dto.NeedPosTarget,
                    TargetPolicy = Parse<SkillTargetPolicy>(dto.TargetPolicy),
                    CastRange = dto.CastRange,
                    CurePotency = dto.CurePotency ?? throw MissingField(dto.Id, "curePotency"),
                    Effect = catalog.GetSkillEffect(DtoOrDefault(dto.Effect, BehaviorIds.SkillEffect.Heal)),
                },
                "hate" => new HateSkillDefinition {
                    SkillId = new SkillKeyId(dto.Id),
                    SpellTime = dto.SpellTime,
                    CooldownTime = dto.CooldownTime,
                    Gcd = MapGcd(dto.Gcd),
                    NeedUnitTarget = dto.NeedUnitTarget,
                    NeedPosTarget = dto.NeedPosTarget,
                    TargetPolicy = Parse<SkillTargetPolicy>(dto.TargetPolicy),
                    CastRange = dto.CastRange,
                    Op = Parse<HateEffectOp>(dto.HateOp),
                    Value = dto.HateValue,
                    Effect = catalog.GetSkillEffect(DtoOrDefault(dto.Effect, BehaviorIds.SkillEffect.Hate)),
                },
                "range_damage" => new RangeDamageSkillDefinition {
                    SkillId = new SkillKeyId(dto.Id),
                    SpellTime = dto.SpellTime,
                    CooldownTime = dto.CooldownTime,
                    Gcd = MapGcd(dto.Gcd),
                    NeedUnitTarget = dto.NeedUnitTarget,
                    NeedPosTarget = dto.NeedPosTarget,
                    TargetPolicy = Parse<SkillTargetPolicy>(dto.TargetPolicy),
                    CastRange = dto.CastRange,
                    CastArea = dto.CastArea is { } area ? MapArea(area) : null,
                    Damage = dto.Damage ?? throw MissingField(dto.Id, "damage"),
                    DamageType = Parse<DamageType>(dto.DamageType),
                    Effect = catalog.GetSkillEffect(DtoOrDefault(dto.Effect, BehaviorIds.SkillEffect.RangeDamage)),
                },
                "add_buff" => new AddBuffSkillDefinition {
                    SkillId = new SkillKeyId(dto.Id),
                    SpellTime = dto.SpellTime,
                    CooldownTime = dto.CooldownTime,
                    Gcd = MapGcd(dto.Gcd),
                    NeedUnitTarget = dto.NeedUnitTarget,
                    NeedPosTarget = dto.NeedPosTarget,
                    TargetPolicy = Parse<SkillTargetPolicy>(dto.TargetPolicy),
                    CastRange = dto.CastRange,
                    Buff = GetBuffByKeyOrThrow(dto.Buff),
                    Effect = catalog.GetSkillEffect(DtoOrDefault(dto.Effect, BehaviorIds.SkillEffect.AddBuff)),
                },
                _ => throw new InvalidOperationException($"技能 '{dto.Id}' 未知 Kind '{dto.Kind}'"),
            };

            _skillsByKey[dto.Id] = skill;
        }
    }

    private static GcdDefinition? MapGcd(GcdContent? dto) {
        if (dto is null)
            return GcdDefinition.Default;
        return new GcdDefinition { GroupKey = string.IsNullOrEmpty(dto.GroupKey) ? null : dto.GroupKey, Time = dto.Time };
    }

    private static RangeShape MapArea(RangeAreaContent dto) {
        return dto.Shape switch {
            "rect" => new RectShape {
                NearClamp = dto.NearClamp,
                FarClamp = dto.FarClamp,
                FromLeft = dto.FromLeft,
                ToRight = dto.ToRight,
            },
            "sector" => new SectorShape {
                NearClamp = dto.NearClamp,
                FarClamp = dto.FarClamp,
                RadianFrom = dto.RadianFrom,
                RadianTo = dto.RadianTo,
            },
            _ => throw new InvalidOperationException($"未知范围形状 '{dto.Shape}'"),
        };
    }
}
