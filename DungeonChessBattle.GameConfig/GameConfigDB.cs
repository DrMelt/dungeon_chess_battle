using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Range;
using DungeonChessBattle.GameConfig.Data;
using DungeonChessBattle.Battle.Domain.Movement;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 纯 C# 配置数据库，直接构建领域只读定义（SkillDefinition / BuffDefinition / RangeShape）。
/// Server 和 Client 直接引用，零反射，编译期类型安全。
/// 通过 IGameConfigDB 接口解耦，消费者可选注入。
/// </summary>
public class GameConfigDB : IGameConfigDB {
    /// <summary>
    /// 全局单例，Godot 脚本通过静态属性访问。
    /// </summary>
    public static readonly GameConfigDB Instance = new();

    /// <summary>魔法持续伤害 Buff 定义。</summary>
    public static DamageOverTimeBuff BuffDotMagic {
        get;
    } = new() {
        BuffTypeId = 1,
        Duration = 30.0,
        MaxStacks = 1,
        DamageType = DamageType.Magic,
        DamagePerSec = 10.0f,
    };

    /// <summary>物理持续伤害 Buff 定义。</summary>
    public static DamageOverTimeBuff BuffDotPhysical {
        get;
    } = new() {
        BuffTypeId = 2,
        Duration = 15.0,
        MaxStacks = 1,
        DamageType = DamageType.Physical,
        DamagePerSec = 100.0f,
    };

    /// <summary>持续治疗 Buff 定义。</summary>
    public static HealOverTimeBuff BuffHot {
        get;
    } = new() {
        BuffTypeId = 3,
        Duration = 15.0,
        MaxStacks = 1,
        HealthPerSec = 100.0f,
    };

    /// <summary>魔法单体伤害技能定义。</summary>
    public static DamageSkillDefinition SkillMagicDamage {
        get;
    } = new() {
        SkillId = new SkillKeyId(1),
        SpellTime = 2.0f,
        CooldownTime = 3.0f,
        GcdTime = 3.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Different,
        Damage = 140.0f,
        DamageType = DamageType.Magic,
    };

    /// <summary>治疗技能定义。</summary>
    public static HealSkillDefinition SkillCure {
        get;
    } = new() {
        SkillId = new SkillKeyId(2),
        SpellTime = 0.5f,
        CooldownTime = 0.5f,
        GcdTime = 2.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Same,
        CurePotency = 500.0f,
    };

    /// <summary>添加魔法持续伤害 Buff 的技能定义。</summary>
    public static AddBuffSkillDefinition SkillAddDotMagic {
        get;
    } = new() {
        SkillId = new SkillKeyId(3),
        SpellTime = 0.0f,
        CooldownTime = 3.0f,
        GcdTime = 3.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Different,
        Buff = BuffDotMagic,
    };

    /// <summary>添加持续治疗 Buff 的技能定义。</summary>
    public static AddBuffSkillDefinition SkillAddHot {
        get;
    } = new() {
        SkillId = new SkillKeyId(4),
        SpellTime = 0.0f,
        CooldownTime = 1.5f,
        GcdTime = 2.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Same,
        Buff = BuffHot,
    };

    /// <summary>矩形范围物理伤害技能定义。</summary>
    public static RangeDamageSkillDefinition SkillRectRangeDamage {
        get;
    } = new() {
        SkillId = new SkillKeyId(5),
        SpellTime = 2.0f,
        CooldownTime = 3.0f,
        GcdTime = 3.0f,
        NeedUnitTarget = false,
        NeedPosTarget = true,
        TargetPolicy = SkillTargetPolicy.Different,
        Damage = 200.0f,
        DamageType = DamageType.Physical,
        Range = new RectShape {
            NearClamp = 0f,
            FarClamp = 5.0f,
        },
    };

    /// <summary>白法师单位配置。</summary>
    public static UnitConfig UnitWhiteMage {
        get;
    } = new() {
        ConfigKey = "WhiteMage",
        BodyRadius = 0.5f,
        MaxHealth = 1000f,
        CureIntensity = 1.0f,
        PhysicalAttackBase = 1.0f,
        PhysicalTakePercent = 1.0f,
        MagicAttackBase = 1.0f,
        MagicTakePercent = 1.0f,
        BaseSpeed = 2.0f,
        Skills =
        [
            SkillAddHot,
            SkillCure,
            SkillAddDotMagic,
            SkillMagicDamage,
            SkillRectRangeDamage,
        ],
    };

    /// <summary>哥布林敌人单位配置。</summary>
    public static UnitConfig UnitGoblin {
        get;
    } = new() {
        ConfigKey = "Goblin",
        BodyRadius = 0.5f,
        MaxHealth = 800f,
        CureIntensity = 1.0f,
        PhysicalAttackBase = 1.2f,
        PhysicalTakePercent = 1.0f,
        MagicAttackBase = 1.0f,
        MagicTakePercent = 1.0f,
        BaseSpeed = 2.2f,
        IsPlayerSelectable = false,
        Skills =
        [
            SkillMagicDamage,
            SkillRectRangeDamage,
        ],
    };

    /// <summary>哥布林首领敌人单位配置。</summary>
    public static UnitConfig UnitGoblinBoss {
        get;
    } = new() {
        ConfigKey = "GoblinBoss",
        BodyRadius = 0.8f,
        MaxHealth = 2000f,
        CureIntensity = 1.0f,
        PhysicalAttackBase = 1.5f,
        PhysicalTakePercent = 0.8f,
        MagicAttackBase = 1.3f,
        MagicTakePercent = 0.8f,
        BaseSpeed = 1.8f,
        IsPlayerSelectable = false,
        Skills =
        [
            SkillAddDotMagic,
            SkillMagicDamage,
            SkillRectRangeDamage,
        ],
    };

    /// <summary>副本一：哥布林营地，少量哥布林投石兵与一名首领。</summary>
    public static DungeonConfig Dungeon_01 {
        get;
    } = new(
        DungeonKey: DungeonRegistry.DefaultDungeonKey,
        DisplayName: "哥布林营地",
        Description: "哥布林们占据了这片林地，消灭它们并击败首领。",
        Enemies: [
            new(Unit: UnitGoblin, Count: 3, SpawnBaseX: 30f, SpawnXSpacing: 3f),
            new(Unit: UnitGoblinBoss, Count: 1, SpawnBaseX: 42f, SpawnXSpacing: 0f),
        ],
        Layout: new BattlefieldLayout(
            50f, 30f,
            [
                new ObstacleRect(MinX: 14f, MinY: 9f, MaxX: 18f, MaxY: 11f),
            ]));

    /// <summary>副本二：深邃洞窟，哥布林群与更强首领。</summary>
    public static DungeonConfig Dungeon_02 {
        get;
    } = new(
        DungeonKey: "dungeon_02",
        DisplayName: "深邃洞窟",
        Description: "洞窟深处的哥布林部落更为凶悍，数量更多。",
        Enemies: [
            new(Unit: UnitGoblin, Count: 5, SpawnBaseX: 28f, SpawnXSpacing: 2.5f),
            new(Unit: UnitGoblinBoss, Count: 1, SpawnBaseX: 44f, SpawnXSpacing: 0f),
        ],
        Layout: new BattlefieldLayout(
            50f, 30f,
            [
                new ObstacleRect(MinX: 8f, MinY: -14f, MaxX: 12f, MaxY: -12f),
                new ObstacleRect(MinX: 18f, MinY: 8f, MaxX: 21f, MaxY: 14f),
            ]));

    DamageOverTimeBuff IGameConfigDB.BuffDotMagic => BuffDotMagic;
    DamageOverTimeBuff IGameConfigDB.BuffDotPhysical => BuffDotPhysical;
    HealOverTimeBuff IGameConfigDB.BuffHot => BuffHot;
    DamageSkillDefinition IGameConfigDB.SkillMagicDamage => SkillMagicDamage;
    HealSkillDefinition IGameConfigDB.SkillCure => SkillCure;
    AddBuffSkillDefinition IGameConfigDB.SkillAddDotMagic => SkillAddDotMagic;
    AddBuffSkillDefinition IGameConfigDB.SkillAddHot => SkillAddHot;
    RangeDamageSkillDefinition IGameConfigDB.SkillRectRangeDamage => SkillRectRangeDamage;
    UnitConfig IGameConfigDB.UnitWhiteMage => UnitWhiteMage;

    /// <summary>
    /// 按技能强类型 ID 查找技能定义。
    /// </summary>
    /// <param name="skillKey">技能配置键。</param>
    /// <returns>对应的技能定义；未找到返回 null。</returns>
    public static SkillDefinition? GetSkillById(SkillKeyId skillKey) {
        return skillKey.Id switch {
            1 => SkillMagicDamage,
            2 => SkillCure,
            3 => SkillAddDotMagic,
            4 => SkillAddHot,
            5 => SkillRectRangeDamage,
            _ => null,
        };
    }

    }
