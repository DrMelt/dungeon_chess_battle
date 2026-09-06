using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.Movement;
using DungeonChessBattle.Battle.Shared.Range;
using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 引擎内置内容基座：现有全部单位/技能/Buff/副本的代码注册版本。
/// 以内置内容为载体直接构造领域定义并注册进 <see cref="ContentSetRegistry"/>，
/// 与 mod 内容同走注册顺序覆盖——内置先注册，mod 后注册天然可覆盖。
/// 数值变更须递增 <see cref="BuiltInRevision"/>，回放门控依赖。
/// </summary>
public static partial class BuiltInContent {
    /// <summary>内置内容修订号：数值/布局任一变化递增，回放门控依赖。</summary>
    public const string BuiltInRevision = "7";

    /// <summary>默认副本键，未覆盖时使用。</summary>
    public const string DefaultDungeonKey = "goblin_camp";

    /// <summary>把全部内置内容注册进内容注册表。行为经行为目录实例化，行为目录须已注册内置行为。</summary>
    public static void Register(ContentSetRegistry registry, BehaviorCatalog catalog) {
        RegisterBuffs(registry, catalog);
        RegisterSkills(registry);
        RegisterUnits(registry, catalog);
        RegisterDungeons(registry, catalog);
        registry.SetDefaultDungeonKey(DefaultDungeonKey);
    }

    private static void RegisterBuffs(ContentSetRegistry registry, BehaviorCatalog catalog) {
        registry.RegisterBuff(new DamageOverTimeBuff {
            BuffTypeId = 1,
            Duration = 30.0,
            MaxStacks = 1,
            DamageType = DamageType.Magic,
            DamagePerSec = 10.0f,
            Effect = catalog.BuffEffect(BehaviorIds.BuffEffect.Dot),
        });
        registry.RegisterBuff(new DamageOverTimeBuff {
            BuffTypeId = 2,
            Duration = 15.0,
            MaxStacks = 1,
            DamageType = DamageType.Physical,
            DamagePerSec = 100.0f,
            Effect = catalog.BuffEffect(BehaviorIds.BuffEffect.Dot),
        });
        registry.RegisterBuff(new HealOverTimeBuff {
            BuffTypeId = 3,
            Duration = 15.0,
            MaxStacks = 1,
            HealthPerSec = 100.0f,
            Effect = catalog.BuffEffect(BehaviorIds.BuffEffect.Hot),
        });
    }
    /// <summary>技能键常量，注册与消费方共用。</summary>
    public static class SkillKeys {
        /// <summary>魔法单体伤害。</summary>
        public const string MagicDamage = "skill_magic_damage";
        /// <summary>单体治疗。</summary>
        public const string Cure = "skill_cure";
        /// <summary>施加魔法持续伤害。</summary>
        public const string AddDotMagic = "skill_add_dot_magic";
        /// <summary>施加持续治疗。</summary>
        public const string AddHot = "skill_add_hot";
        /// <summary>矩形范围物理伤害。</summary>
        public const string RectRangeDamage = "skill_rect_range_damage";
        /// <summary>单体嘲讽。</summary>
        public const string Taunt = "skill_taunt";
    }

    private static void RegisterSkills(ContentSetRegistry registry) {
        var skills = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal) {
            [SkillKeys.MagicDamage] = new DamageSkillDefinition {
                SkillId = new SkillKeyId(SkillKeys.MagicDamage),
                SpellTime = 2.0f,
                CooldownTime = 0.0f,
                Gcd = GcdDefinition.Default,
                NeedUnitTarget = true,
                NeedPosTarget = false,
                TargetPolicy = SkillTargetPolicy.Different,
                CastRange = 10f,
                Damage = 140.0f,
                DamageType = DamageType.Magic,
                Effect = new Skills.DamageEffect(),
            },
            [SkillKeys.Cure] = new HealSkillDefinition {
                SkillId = new SkillKeyId(SkillKeys.Cure),
                SpellTime = 0.5f,
                CooldownTime = 0.0f,
                Gcd = GcdDefinition.Default,
                NeedUnitTarget = true,
                NeedPosTarget = false,
                TargetPolicy = SkillTargetPolicy.Same,
                CastRange = 8f,
                CurePotency = 500.0f,
                Effect = new Skills.HealEffect(),
            },
            [SkillKeys.AddDotMagic] = new AddBuffSkillDefinition {
                SkillId = new SkillKeyId(SkillKeys.AddDotMagic),
                SpellTime = 0.0f,
                CooldownTime = 0.0f,
                Gcd = GcdDefinition.Default,
                NeedUnitTarget = true,
                NeedPosTarget = false,
                TargetPolicy = SkillTargetPolicy.Different,
                CastRange = 10f,
                Buff = registry.GetBuff(1)!,
                Effect = new Skills.AddBuffEffect(),
            },
            [SkillKeys.AddHot] = new AddBuffSkillDefinition {
                SkillId = new SkillKeyId(SkillKeys.AddHot),
                SpellTime = 0.0f,
                CooldownTime = 0.0f,
                Gcd = GcdDefinition.Default,
                NeedUnitTarget = true,
                NeedPosTarget = false,
                TargetPolicy = SkillTargetPolicy.Same,
                CastRange = 8f,
                Buff = registry.GetBuff(3)!,
                Effect = new Skills.AddBuffEffect(),
            },
            [SkillKeys.RectRangeDamage] = new RangeDamageSkillDefinition {
                SkillId = new SkillKeyId(SkillKeys.RectRangeDamage),
                SpellTime = 2.0f,
                CooldownTime = 0.0f,
                Gcd = GcdDefinition.Default,
                NeedUnitTarget = false,
                NeedPosTarget = true,
                TargetPolicy = SkillTargetPolicy.Different,
                CastArea = new RectShape { NearClamp = 0f, FarClamp = 5.0f },
                Damage = 200.0f,
                DamageType = DamageType.Physical,
                Effect = new Skills.RangeDamageEffect(),
            },
            [SkillKeys.Taunt] = new HateSkillDefinition {
                SkillId = new SkillKeyId(SkillKeys.Taunt),
                SpellTime = 0.0f,
                CooldownTime = 20.0f,
                Gcd = new GcdDefinition { GroupKey = null, Time = 2.5f },
                NeedUnitTarget = true,
                NeedPosTarget = false,
                TargetPolicy = SkillTargetPolicy.Different,
                CastRange = 10f,
                Op = HateEffectOp.SetTop,
                Value = 1000.0f,
                Effect = new Skills.HateSkillEffect(),
            }
        };

        foreach (var skill in skills.Values)
            registry.RegisterSkill(skill);
    }

    private static void RegisterUnits(ContentSetRegistry registry, BehaviorCatalog catalog) {
        registry.RegisterUnit(new UnitConfig {
            ConfigKey = "WhiteMage",
            IsPlayerSelectable = true,
            BaseConfig = new UnitBaseConfig(
                MaxHealth: 1000f, BodyRadius: 0.5f, BaseSpeed: 2.0f,
                PhysicalAttackBase: 1.0f, PhysicalTakePercent: 1.0f,
                MagicAttackBase: 1.0f, MagicTakePercent: 1.0f, CureIntensity: 1.0f),
            Skills = [
                registry.GetSkill(new SkillKeyId(SkillKeys.AddHot))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.Cure))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.AddDotMagic))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.MagicDamage))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.RectRangeDamage))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.Taunt))!,
            ],
            HateRule = catalog.HateRule(BehaviorIds.HateRule.Default),
            HateFactor = 0.8f,
        });

        registry.RegisterUnit(new UnitConfig {
            ConfigKey = "GoblinBoss",
            IsPlayerSelectable = false,
            BaseConfig = new UnitBaseConfig(
                MaxHealth: 2000f, BodyRadius: 0.8f, BaseSpeed: 1.8f,
                PhysicalAttackBase: 1.5f, PhysicalTakePercent: 0.8f,
                MagicAttackBase: 1.3f, MagicTakePercent: 0.8f, CureIntensity: 1.0f),
            Skills = [
                registry.GetSkill(new SkillKeyId(SkillKeys.AddDotMagic))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.MagicDamage))!,
                registry.GetSkill(new SkillKeyId(SkillKeys.RectRangeDamage))!,
            ],
            Intelligence = catalog.Intelligence(BehaviorIds.Intelligence.EnemyBasic),
            HateRule = catalog.HateRule(BehaviorIds.HateRule.Default),
            HateFactor = 1.0f,
        });
    }

    private static void RegisterDungeons(ContentSetRegistry registry, BehaviorCatalog catalog) {
        var goblin = registry.GetUnit("Goblin")!;
        var goblinBoss = registry.GetUnit("GoblinBoss")!;
        var relations = catalog.CampRelation(BehaviorIds.CampRelation.PveBoss);

        registry.RegisterDungeon(new DungeonConfig(
            DungeonKey: "goblin_camp",
            PlayerCampOptions: [new PlayerCampOption("a", ["Camp_A"])],
            Enemies: [
                new EnemySpawnConfig(goblin, Count: 3, SpawnBaseX: 30f, SpawnXSpacing: 3f),
                new EnemySpawnConfig(goblinBoss, Count: 1, SpawnBaseX: 42f, SpawnXSpacing: 0f),
            ],
            RelationsResolver: relations,
            EnemyCamps: ["Camp_BOSS"],
            Layout: new BattlefieldLayout(
                50f, 30f, [new ObstacleRect(14f, 9f, 18f, 11f)])));

        registry.RegisterDungeon(new DungeonConfig(
            DungeonKey: "deep_cave",
            PlayerCampOptions: [new PlayerCampOption("a", ["Camp_A"])],
            Enemies: [
                new EnemySpawnConfig(goblin, Count: 5, SpawnBaseX: 28f, SpawnXSpacing: 2.5f),
                new EnemySpawnConfig(goblinBoss, Count: 1, SpawnBaseX: 44f, SpawnXSpacing: 0f),
            ],
            RelationsResolver: relations,
            EnemyCamps: ["Camp_BOSS"],
            Layout: new BattlefieldLayout(
                50f, 30f,
                [
                    new ObstacleRect(8f, -14f, 12f, -12f),
                    new ObstacleRect(18f, 8f, 21f, 14f),
                ])));
    }
}
