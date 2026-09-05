using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 引擎内置内容基座：现有全部单位/技能/Buff/副本的数据化版本。
/// 以生成 <see cref="ModContentJson"/> 的形式存在，与用户 mod 同走 ModLoader 合并管线，
/// 可被任意 mod 按键覆盖。内容变更须递增 <see cref="BuiltInRevision"/>，回放门控依赖。
/// </summary>
public static partial class BuiltInContent {
    /// <summary>内置内容修订号：数值/布局任一变化递增，回放门控依赖。</summary>
    public const string BuiltInRevision = "6";

    /// <summary>默认副本键，未声明时使用。</summary>
    public const string DefaultDungeonKey = "goblin_camp";

    /// <summary>生成内置内容数据，每次调用返回新实例，合并管线可安全覆盖。</summary>
    public static ModContentJson Create() => new() {
        Buffs = [
            new() {
                Id = "buff_magic_dot", BuffTypeId = 1, Kind = "dot", Duration = 30.0, MaxStacks = 1,
                DamageType = "Magic", DamagePerSec = 10.0f,
            },
            new() {
                Id = "buff_physical_dot", BuffTypeId = 2, Kind = "dot", Duration = 15.0, MaxStacks = 1,
                DamageType = "Physical", DamagePerSec = 100.0f,
            },
            new() {
                Id = "buff_hot", BuffTypeId = 3, Kind = "hot", Duration = 15.0, MaxStacks = 1,
                HealthPerSec = 100.0f,
            },
        ],
        Skills = [
            new() {
                Id = "skill_magic_damage", Kind = "damage", SpellTime = 2.0f, CooldownTime = 0.0f,
                NeedUnitTarget = true, TargetPolicy = "Different", CastRange = 10f, Damage = 140.0f, DamageType = "Magic",
            },
            new() {
                Id = "skill_cure", Kind = "heal", SpellTime = 0.5f, CooldownTime = 0.0f,
                NeedUnitTarget = true, TargetPolicy = "Same", CastRange = 8f, CurePotency = 500.0f,
            },
            new() {
                Id = "skill_add_dot_magic", Kind = "add_buff", SpellTime = 0.0f, CooldownTime = 0.0f,
                NeedUnitTarget = true, TargetPolicy = "Different", CastRange = 10f, Buff = "buff_magic_dot",
            },
            new() {
                Id = "skill_add_hot", Kind = "add_buff", SpellTime = 0.0f, CooldownTime = 0.0f,
                NeedUnitTarget = true, TargetPolicy = "Same", CastRange = 8f, Buff = "buff_hot",
            },
            new() {
                Id = "skill_rect_range_damage", Kind = "range_damage", SpellTime = 2.0f, CooldownTime = 0.0f,
                NeedPosTarget = true, TargetPolicy = "Different", Damage = 200.0f, DamageType = "Physical",
                CastArea = new RangeAreaContent { Shape = "rect", NearClamp = 0f, FarClamp = 5.0f },
            },
            new() {
                Id = "skill_taunt", Kind = "hate", SpellTime = 0.0f, CooldownTime = 20.0f,
                Gcd = new GcdContent { GroupKey = null, Time = 2.5f },
                NeedUnitTarget = true, TargetPolicy = "Different", CastRange = 10f, HateOp = "SetTop", HateValue = 1000.0f,
            },
        ],
        Units = [
            new() {
                ConfigKey = "WhiteMage", IsPlayerSelectable = true,
                MaxHealth = 1000f, BodyRadius = 0.5f, BaseSpeed = 2.0f,
                PhysicalAttackBase = 1.0f, PhysicalTakePercent = 1.0f, MagicAttackBase = 1.0f, MagicTakePercent = 1.0f, CureIntensity = 1.0f,
                Skills = ["skill_add_hot", "skill_cure", "skill_add_dot_magic", "skill_magic_damage", "skill_rect_range_damage", "skill_taunt"],
                HateRule = "hate.default", HateFactor = 0.8f,
            },
            new() {
                ConfigKey = "Goblin", IsPlayerSelectable = false,
                MaxHealth = 800f, BodyRadius = 0.5f, BaseSpeed = 2.2f,
                PhysicalAttackBase = 1.2f, PhysicalTakePercent = 1.0f, MagicAttackBase = 1.0f, MagicTakePercent = 1.0f, CureIntensity = 1.0f,
                Skills = ["skill_magic_damage", "skill_rect_range_damage"],
                Intelligence = "ai.enemy_basic", HateRule = "hate.default", HateFactor = 1.0f,
            },
            new() {
                ConfigKey = "GoblinBoss", IsPlayerSelectable = false,
                MaxHealth = 2000f, BodyRadius = 0.8f, BaseSpeed = 1.8f,
                PhysicalAttackBase = 1.5f, PhysicalTakePercent = 0.8f, MagicAttackBase = 1.3f, MagicTakePercent = 0.8f, CureIntensity = 1.0f,
                Skills = ["skill_add_dot_magic", "skill_magic_damage", "skill_rect_range_damage"],
                Intelligence = "ai.enemy_basic", HateRule = "hate.default", HateFactor = 1.0f,
            },
        ],
        Dungeons = [
            new() {
                Key = "goblin_camp",
                PlayerCamps = [new() { Key = "a", Camps = ["Camp_A"] }],
                EnemyCamps = ["Camp_BOSS"],
                Enemies = [
                    new() { Unit = "Goblin", Count = 3, SpawnBaseX = 30f, SpawnXSpacing = 3f },
                    new() { Unit = "GoblinBoss", Count = 1, SpawnBaseX = 42f, SpawnXSpacing = 0f },
                ],
                Relations = "camp.pve_boss",
                Layout = new LayoutContent {
                    HalfWidth = 50f, HalfHeight = 30f,
                    Obstacles = [new() { MinX = 14f, MinY = 9f, MaxX = 18f, MaxY = 11f }],
                },
            },
            new() {
                Key = "deep_cave",
                PlayerCamps = [new() { Key = "a", Camps = ["Camp_A"] }],
                EnemyCamps = ["Camp_BOSS"],
                Enemies = [
                    new() { Unit = "Goblin", Count = 5, SpawnBaseX = 28f, SpawnXSpacing = 2.5f },
                    new() { Unit = "GoblinBoss", Count = 1, SpawnBaseX = 44f, SpawnXSpacing = 0f },
                ],
                Relations = "camp.pve_boss",
                Layout = new LayoutContent {
                    HalfWidth = 50f, HalfHeight = 30f,
                    Obstacles = [
                        new() { MinX = 8f, MinY = -14f, MaxX = 12f, MaxY = -12f },
                        new() { MinX = 18f, MinY = 8f, MaxX = 21f, MaxY = 14f },
                    ],
                },
            },
        ],
        DefaultDungeonKey = DefaultDungeonKey,
    };
}
