using DungeonChessBattle.Battle.Domain.Combat;
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
        Id = 1,
        Duration = 30.0,
        MaxSuperpositions = 1,
        DamageType = DamageType.Magic,
        DamagePerSec = 10.0f,
    };

    /// <summary>物理持续伤害 Buff 配置。</summary>
    public static BuffDOTConfig BuffDotPhysical {
        get;
    } = new() {
        Id = 2,
        Duration = 15.0,
        MaxSuperpositions = 1,
        DamageType = DamageType.Physical,
        DamagePerSec = 100.0f,
    };

    /// <summary>持续治疗 Buff 配置。</summary>
    public static BuffHOTConfig BuffHot {
        get;
    } = new() {
        Id = 3,
        Duration = 15.0,
        MaxSuperpositions = 1,
        HealthPerSec = 100.0f,
    };

    /// <summary>魔法单体伤害技能配置。</summary>
    public static SkillDamageConfig SkillMagicDamage {
        get;
    } = new() {
        Id = 1,
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
        Id = 2,
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
        Id = 3,
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
        Id = 4,
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
        Id = 5,
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

    BuffDOTConfig IGameConfigDB.BuffDotMagic => BuffDotMagic;
    BuffDOTConfig IGameConfigDB.BuffDotPhysical => BuffDotPhysical;
    BuffHOTConfig IGameConfigDB.BuffHot => BuffHot;
    SkillDamageConfig IGameConfigDB.SkillMagicDamage => SkillMagicDamage;
    SkillCureConfig IGameConfigDB.SkillCure => SkillCure;
    SkillAddBuffConfig IGameConfigDB.SkillAddDotMagic => SkillAddDotMagic;
    SkillAddBuffConfig IGameConfigDB.SkillAddHot => SkillAddHot;
    SkillRangeDamageConfig IGameConfigDB.SkillRectRangeDamage => SkillRectRangeDamage;
    UnitConfig IGameConfigDB.UnitWhiteMage => UnitWhiteMage;

    /// <summary>
    /// 按技能全局 ID 查找技能配置。
    /// </summary>
    /// <param name="skillId">技能配置 ID。</param>
    /// <returns>对应的技能配置；未找到返回 null。</returns>
    public static SkillConfig? GetSkillById(ushort skillId) {
        return skillId switch {
            1 => SkillMagicDamage,
            2 => SkillCure,
            3 => SkillAddDotMagic,
            4 => SkillAddHot,
            5 => SkillRectRangeDamage,
            _ => null,
        };
    }

}
