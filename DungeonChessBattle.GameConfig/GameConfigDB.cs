using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Range;
using DungeonChessBattle.GameConfig.Models;
using DungeonChessBattle.Battle.Shared.Movement;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.GameConfig.Intelligence;
using DungeonChessBattle.GameConfig.Buffs;
using DungeonChessBattle.GameConfig.Skills;

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

    /// <summary>
    /// 战斗数据集修订号，只背内容与布局侧：单位数值/技能/Buff/伤害治疗公式/敌人决策算法/仇恨/阵营/副本布局。
    /// 引擎侧的结算时序与事件顺序由 <see cref="DungeonChessBattle.Battle.Shared.Combat.BattleLogicRevision"/> 背。
    /// 任何影响战斗结果的变更都必须递增对应修订号；回放端两项都要与本地比对，任一不符直接拒绝重放。
    /// </summary>
    public const string DataRevision = "5";

    /// <summary>魔法持续伤害 Buff 定义。</summary>
    public static DamageOverTimeBuff BuffDotMagic {
        get;
    } = new() {
        BuffTypeId = 1,
        Duration = 30.0,
        MaxStacks = 1,
        DamageType = DamageType.Magic,
        DamagePerSec = 10.0f,
        Effect = new DotEffect(),
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
        Effect = new DotEffect(),
    };

    /// <summary>持续治疗 Buff 定义。</summary>
    public static HealOverTimeBuff BuffHot {
        get;
    } = new() {
        BuffTypeId = 3,
        Duration = 15.0,
        MaxStacks = 1,
        HealthPerSec = 100.0f,
        Effect = new HotEffect(),
    };

    /// <summary>魔法单体伤害技能定义。</summary>
    public static DamageSkillDefinition SkillMagicDamage {
        get;
    } = new() {
        SkillId = new SkillKeyId("skill_magic_damage"),
        SpellTime = 2.0f,
        CooldownTime = 0.0f,
        GcdTime = 2.5f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Different,
        CastRange = 10f,
        Damage = 140.0f,
        DamageType = DamageType.Magic,
        Effect = new DamageEffect(),
    };

    /// <summary>治疗技能定义。</summary>
    public static HealSkillDefinition SkillCure {
        get;
    } = new() {
        SkillId = new SkillKeyId("skill_cure"),
        SpellTime = 0.5f,
        CooldownTime = 0.0f,
        GcdTime = 2.5f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Same,
        CastRange = 8f,
        CurePotency = 500.0f,
        Effect = new HealEffect(),
    };

    /// <summary>添加魔法持续伤害 Buff 的技能定义。</summary>
    public static AddBuffSkillDefinition SkillAddDotMagic {
        get;
    } = new() {
        SkillId = new SkillKeyId("skill_add_dot_magic"),
        SpellTime = 0.0f,
        CooldownTime = 0.0f,
        GcdTime = 2.5f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Different,
        CastRange = 10f,
        Buff = BuffDotMagic,
        Effect = new AddBuffEffect(),
    };

    /// <summary>添加持续治疗 Buff 的技能定义。</summary>
    public static AddBuffSkillDefinition SkillAddHot {
        get;
    } = new() {
        SkillId = new SkillKeyId("skill_add_hot"),
        SpellTime = 0.0f,
        CooldownTime = 0.0f,
        GcdTime = 2.5f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Same,
        CastRange = 8f,
        Buff = BuffHot,
        Effect = new AddBuffEffect(),
    };

    /// <summary>矩形范围物理伤害技能定义。</summary>
    public static RangeDamageSkillDefinition SkillRectRangeDamage {
        get;
    } = new() {
        SkillId = new SkillKeyId("skill_rect_range_damage"),
        SpellTime = 2.0f,
        CooldownTime = 0.0f,
        GcdTime = 2.5f,
        NeedUnitTarget = false,
        NeedPosTarget = true,
        TargetPolicy = SkillTargetPolicy.Different,
        Damage = 200.0f,
        DamageType = DamageType.Physical,
        CastArea = new RectShape {
            NearClamp = 0f,
            FarClamp = 5.0f,
        },
        Effect = new RangeDamageEffect(),
    };

    /// <summary>单体嘲讽仇恨技能定义：把目标敌人对本单位的仇恨抬到最高之上。</summary>
    public static HateSkillDefinition SkillTaunt {
        get;
    } = new() {
        SkillId = new SkillKeyId("skill_taunt"),
        SpellTime = 0.5f,
        CooldownTime = 0.0f,
        GcdTime = 2.5f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        TargetPolicy = SkillTargetPolicy.Different,
        CastRange = 5f,
        Op = HateEffectOp.SetTop,
        Value = 1000.0f,
        Effect = new HateSkillEffect(),
    };

    /// <summary>白法师单位配置。</summary>
    public static UnitConfig UnitWhiteMage {
        get;
    } = new() {
        ConfigKey = "WhiteMage",
        HateFactor = 0.8f,
        HateRule = new DefaultHateRule(),
        BaseConfig = new UnitBaseConfig(
            MaxHealth: 1000f,
            BodyRadius: 0.5f,
            BaseSpeed: 2.0f,
            PhysicalAttackBase: 1.0f,
            PhysicalTakePercent: 1.0f,
            MagicAttackBase: 1.0f,
            MagicTakePercent: 1.0f,
            CureIntensity: 1.0f),
        Skills =
        [
            SkillAddHot,
            SkillCure,
            SkillAddDotMagic,
            SkillMagicDamage,
            SkillRectRangeDamage,
            SkillTaunt,
        ],
    };

    /// <summary>哥布林敌人单位配置。</summary>
    public static UnitConfig UnitGoblin {
        get;
    } = new() {
        ConfigKey = "Goblin",
        IsPlayerSelectable = false,
        HateRule = new DefaultHateRule(),
        Intelligence = new EnemyIntelligence(),
        BaseConfig = new UnitBaseConfig(
            MaxHealth: 800f,
            BodyRadius: 0.5f,
            BaseSpeed: 2.2f,
            PhysicalAttackBase: 1.2f,
            PhysicalTakePercent: 1.0f,
            MagicAttackBase: 1.0f,
            MagicTakePercent: 1.0f,
            CureIntensity: 1.0f),
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
        IsPlayerSelectable = false,
        HateRule = new DefaultHateRule(),
        Intelligence = new EnemyIntelligence(),
        BaseConfig = new UnitBaseConfig(
            MaxHealth: 2000f,
            BodyRadius: 0.8f,
            BaseSpeed: 1.8f,
            PhysicalAttackBase: 1.5f,
            PhysicalTakePercent: 0.8f,
            MagicAttackBase: 1.3f,
            MagicTakePercent: 0.8f,
            CureIntensity: 1.0f),
        Skills =
        [
            SkillAddDotMagic,
            SkillMagicDamage,
            SkillRectRangeDamage,
        ],
    };

    /// <summary>默认副本键，创建房间未指定副本时使用。</summary>
    public const string DefaultDungeonKey = "goblin_camp";

    /// <summary>副本一：哥布林营地，少量哥布林投石兵与一名首领。</summary>
    public static DungeonConfig DungeonGoblinCamp {
        get;
    } = new(
        DungeonKey: DefaultDungeonKey,
        PlayerCampOptions: [new(CampConstants.CampA, [CampConstants.CampA])],
        EnemyCamps: [CampConstants.CampBoss],
        Enemies: [
            new(Unit: UnitGoblin, Count: 3, SpawnBaseX: 30f, SpawnXSpacing: 3f),
            new(Unit: UnitGoblinBoss, Count: 1, SpawnBaseX: 42f, SpawnXSpacing: 0f),
        ],
        RelationsResolver: CampRelationsPve,
        Layout: new BattlefieldLayout(
            50f, 30f,
            [
                new ObstacleRect(MinX: 14f, MinY: 9f, MaxX: 18f, MaxY: 11f),
            ]));

    /// <summary>副本二：深邃洞窟，哥布林群与更强首领。</summary>
    public static DungeonConfig DungeonDeepCave {
        get;
    } = new(
        DungeonKey: "deep_cave",
        PlayerCampOptions: [new(CampConstants.CampA, [CampConstants.CampA])],
        EnemyCamps: [CampConstants.CampBoss],
        Enemies: [
            new(Unit: UnitGoblin, Count: 5, SpawnBaseX: 28f, SpawnXSpacing: 2.5f),
            new(Unit: UnitGoblinBoss, Count: 1, SpawnBaseX: 44f, SpawnXSpacing: 0f),
        ],
        RelationsResolver: CampRelationsPve,
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
            "skill_magic_damage" => SkillMagicDamage,
            "skill_cure" => SkillCure,
            "skill_add_dot_magic" => SkillAddDotMagic,
            "skill_add_hot" => SkillAddHot,
            "skill_rect_range_damage" => SkillRectRangeDamage,
            "skill_taunt" => SkillTaunt,
            _ => null,
        };
    }

    /// <summary>
    /// 副本关系函数：双方同属 Boss 阵营为友；任一方含 Boss 阵营即敌对；双方存在共同阵营即友；其余组合返回 Unknown 不猜。
    /// 支持多阵营：玩家可同属多个阵营，任一共同阵营即视同友方（A、B 玩家组队共斗）。
    /// </summary>
    private static CampRelation CampRelationsPve(
        IReadOnlyList<string> sourceCamps, IReadOnlyList<string> targetCamps) {
        bool sourceHasBoss = false;
        bool targetHasBoss = false;
        foreach (var camp in sourceCamps) {
            if (camp == CampConstants.CampBoss)
                sourceHasBoss = true;
        }
        foreach (var camp in targetCamps) {
            if (camp == CampConstants.CampBoss)
                targetHasBoss = true;
        }
        if (sourceHasBoss && targetHasBoss)
            return CampRelation.Friendly;
        if (sourceHasBoss || targetHasBoss)
            return CampRelation.Enemy;
        foreach (var camp in sourceCamps) {
            foreach (var other in targetCamps) {
                if (camp == other)
                    return CampRelation.Friendly;
            }
        }
        return CampRelation.Unknown;
    }

}
