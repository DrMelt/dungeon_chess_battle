using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Range;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 纯 C# 配置数据库，数值对齐客户端 .tres 资源文件。
/// Server 和 Client 直接引用，零反射，编译期类型安全。
/// 通过 IGameConfigDB 接口解耦，消费者可选注入。
/// </summary>
public class GameConfigDB : IGameConfigDB {
    /// <summary>
    /// 全局单例，Godot 脚本通过静态属性访问。
    /// </summary>
    public static readonly GameConfigDB Instance = new();

    /// <summary>魔法持续伤害 Buff 配置。</summary>
    public static BuffDOTConfig BuffDotMagic {
        get;
    } = new() {
        Duration = 30.0,
        MaxSuperpositions = 1,
        DamageType = DamageType.Magic,
        DamagePerSec = 10.0f,
    };

    /// <summary>物理持续伤害 Buff 配置。</summary>
    public static BuffDOTConfig BuffDotPhysical {
        get;
    } = new() {
        Duration = 15.0,
        MaxSuperpositions = 1,
        DamageType = DamageType.Physical,
        DamagePerSec = 100.0f,
    };

    /// <summary>持续治疗 Buff 配置。</summary>
    public static BuffHOTConfig BuffHot {
        get;
    } = new() {
        Duration = 15.0,
        MaxSuperpositions = 1,
        HealthPerSec = 100.0f,
    };

    /// <summary>魔法单体伤害技能配置。</summary>
    public static SkillDamageConfig SkillMagicDamage {
        get;
    } = new() {
        SkillSpellTime = 2.0f,
        SkillCooldownTime = 3.0f,
        GCDTime = 3.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Different",
        Damage = 140.0f,
        DamageType = DamageType.Magic,
    };

    /// <summary>治疗技能配置。</summary>
    public static SkillCureConfig SkillCure {
        get;
    } = new() {
        SkillSpellTime = 0.5f,
        SkillCooldownTime = 0.5f,
        GCDTime = 2.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Same",
        CurePotency = 500.0f,
    };

    /// <summary>添加魔法持续伤害 Buff 的技能配置。</summary>
    public static SkillAddBuffConfig SkillAddDotMagic {
        get;
    } = new() {
        SkillSpellTime = 0.0f,
        SkillCooldownTime = 3.0f,
        GCDTime = 3.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Different",
        BuffConfig = BuffDotMagic,
    };

    /// <summary>添加持续治疗 Buff 的技能配置。</summary>
    public static SkillAddBuffConfig SkillAddHot {
        get;
    } = new() {
        SkillSpellTime = 0.0f,
        SkillCooldownTime = 1.5f,
        GCDTime = 2.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Same",
        BuffConfig = BuffHot,
    };

    /// <summary>矩形范围物理伤害技能配置。</summary>
    public static SkillRangeDamageConfig SkillRectRangeDamage {
        get;
    } = new() {
        SkillSpellTime = 2.0f,
        SkillCooldownTime = 3.0f,
        GCDTime = 3.0f,
        NeedPosTarget = true,
        Damage = 200.0f,
        DamageType = DamageType.Physical,
        Range = new RectRangeConfig {
            FarClamp = 5.0f,
        },
    };

    /// <summary>白法师单位配置。</summary>
    public static UnitConfig UnitWhiteMage {
        get;
    } = new() {
        BodyRadius = 1.0f,
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

    BuffDOTConfig IGameConfigDB.BuffDotMagic => BuffDotMagic;
    BuffDOTConfig IGameConfigDB.BuffDotPhysical => BuffDotPhysical;
    BuffHOTConfig IGameConfigDB.BuffHot => BuffHot;
    SkillDamageConfig IGameConfigDB.SkillMagicDamage => SkillMagicDamage;
    SkillCureConfig IGameConfigDB.SkillCure => SkillCure;
    SkillAddBuffConfig IGameConfigDB.SkillAddDotMagic => SkillAddDotMagic;
    SkillAddBuffConfig IGameConfigDB.SkillAddHot => SkillAddHot;
    SkillRangeDamageConfig IGameConfigDB.SkillRectRangeDamage => SkillRectRangeDamage;
    UnitConfig IGameConfigDB.UnitWhiteMage => UnitWhiteMage;

    UnitModel IGameConfigDB.ToUnitModel(UnitConfig config) => ToUnitModel(config);
    SkillModel IGameConfigDB.ToSkillModel(SkillConfig config) => ToSkillModel(config);
    BuffModel IGameConfigDB.ToBuffModel(BuffConfig config) => ToBuffModel(config);

    /// <summary>
    /// 将单位配置转换为运行时单位模型。
    /// </summary>
    /// <param name="config">单位配置。</param>
    /// <returns>对应的 <see cref="UnitModel"/>。</returns>
    public static UnitModel ToUnitModel(UnitConfig config) {
        ArgumentNullException.ThrowIfNull(config);

        return new UnitModel {
            BodyRadius = config.BodyRadius,
            MaxHealth = config.MaxHealth,
            CureIntensity = config.CureIntensity,
            PhysicalAttackBase = config.PhysicalAttackBase,
            PhysicalTakePercent = config.PhysicalTakePercent,
            MagicAttackBase = config.MagicAttackBase,
            MagicTakePercent = config.MagicTakePercent,
            BaseSpeed = config.BaseSpeed,
        };
    }

    /// <summary>
    /// 将技能配置转换为对应类型的运行时技能模型。
    /// </summary>
    /// <param name="config">技能配置。</param>
    /// <returns>对应的 <see cref="SkillModel"/> 派生实例。</returns>
    public static SkillModel ToSkillModel(SkillConfig config) {
        ArgumentNullException.ThrowIfNull(config);

        var model = config switch {
            SkillDamageConfig dmg => (SkillModel)new SkillDamageModel {
                Damage = dmg.Damage,
                DamageType = dmg.DamageType,
            },
            SkillCureConfig cure => new SkillCureModel {
                CurePotency = cure.CurePotency,
            },
            SkillAddBuffConfig addBuff => new SkillAddBuffModel {
                Buff = ToBuffModel(addBuff.BuffConfig),
            },
            SkillRangeDamageConfig rangeDmg => new SkillRangeDamageModel {
                Damage = rangeDmg.Damage,
                DamageType = rangeDmg.DamageType,
                RangeRes = ToRangeRes(rangeDmg.Range),
            },
            _ => throw new InvalidOperationException(
                $"Unknown SkillConfig type: {config.GetType().Name}. " +
                "Please add the corresponding case in GameConfigDB.ToSkillModel()."),
        };

        model.SkillSpellTime = config.SkillSpellTime;
        model.SkillCooldownTime = config.SkillCooldownTime;
        model.GCDTime = config.GCDTime;
        model.NeedUnitTarget = config.NeedUnitTarget;
        model.NeedPosTarget = config.NeedPosTarget;
        model.SkillCanAdd = Enum.Parse<SkillCanAdd>(config.SkillCanAdd);

        return model;
    }

    /// <summary>
    /// 将 Buff 配置转换为对应类型的运行时 Buff 模型。
    /// </summary>
    /// <param name="config">Buff 配置。</param>
    /// <returns>对应的 <see cref="BuffModel"/> 派生实例。</returns>
    public static BuffModel ToBuffModel(BuffConfig config) {
        ArgumentNullException.ThrowIfNull(config);

        var model = config switch {
            BuffDOTConfig dot => (BuffModel)new BuffDOTModel {
                DamageType = dot.DamageType,
                DamagePerSec = dot.DamagePerSec,
            },
            BuffHOTConfig hot => new BuffHOTModel {
                HealthPerSec = hot.HealthPerSec,
            },
            _ => throw new InvalidOperationException(
                $"Unknown BuffConfig type: {config.GetType().Name}. " +
                "Please add the corresponding case in GameConfigDB.ToBuffModel()."),
        };

        model.Duration = config.Duration;
        model.MaxSuperpositions = config.MaxSuperpositions;

        return model;
    }

    /// <summary>
    /// 将范围配置转换为对应的范围判定器。
    /// </summary>
    /// <param name="config">范围配置。</param>
    /// <returns>对应的 <see cref="IRangeChecker"/> 实现。</returns>
    private static IRangeChecker ToRangeRes(RangeConfig config) {
        ArgumentNullException.ThrowIfNull(config);

        return config switch {
            CircularRangeConfig c => new CircularRangeChecker {
                NearClamp = c.NearClamp,
                FarClamp = c.FarClamp,
                RadianFrom = c.RadianFrom,
                RadianTo = c.RadianTo,
            },
            RectRangeConfig r => new RectRangeChecker {
                NearClamp = r.NearClamp,
                FarClamp = r.FarClamp,
                FromL = r.FromL,
                ToR = r.ToR,
            },
            _ => throw new InvalidOperationException(
                $"Unknown RangeConfig type: {config.GetType().Name}. " +
                "Please add the corresponding case in GameConfigDB.ToRangeRes()."),
        };
    }
}
