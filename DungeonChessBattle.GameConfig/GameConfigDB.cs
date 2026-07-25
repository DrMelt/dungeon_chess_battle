using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Range;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 纯 C# 静态配置数据库，数值对齐客户端 .tres 资源文件。
/// Server 和 Client 直接引用，零反射，编译期类型安全。
/// </summary>
public static class GameConfigDB {
    // ── Skills ──

    public static SkillDamageConfig SkillMagicDamage {
        get;
    } = new() {
        Id = "skill_magic_damage",
        SkillName = "魔法攻击",
        SkillDescription = "对目标发动魔法攻击\n威力：140",
        SkillSpellTime = 2.0f,
        SkillCooldownTime = 3.0f,
        GCDTime = 3.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Different",
        Damage = 140.0f,
        DamageType = "Magic",
    };

    public static SkillCureConfig SkillCure {
        get;
    } = new() {
        Id = "skill_cure",
        SkillName = "治疗",
        SkillDescription = "回复目标体力\n恢复力：500",
        SkillSpellTime = 0.5f,
        SkillCooldownTime = 0.5f,
        GCDTime = 2.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Same",
        CurePotency = 500.0f,
    };

    public static SkillAddBuffConfig SkillAddDotMagic {
        get;
    } = new() {
        Id = "skill_add_dot_magic",
        SkillName = "持续魔法伤害",
        SkillDescription = "对目标添加持续魔法伤害buff\n威力：10\n持续时间：30秒",
        SkillSpellTime = 0.0f,
        SkillCooldownTime = 3.0f,
        GCDTime = 3.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Different",
        BuffId = "buff_dot_magic",
    };

    public static SkillAddBuffConfig SkillAddHot {
        get;
    } = new() {
        Id = "skill_add_hot",
        SkillName = "持续治疗",
        SkillDescription = "对目标添加持续治疗buff\n恢复力：100\n持续时间：15秒",
        SkillSpellTime = 0.0f,
        SkillCooldownTime = 1.5f,
        GCDTime = 2.0f,
        NeedUnitTarget = true,
        NeedPosTarget = false,
        SkillCanAdd = "Same",
        BuffId = "buff_hot",
    };

    public static SkillRangeDamageConfig SkillRectRangeDamage {
        get;
    } = new() {
        Id = "skill_rect_range_damage",
        SkillName = "RectRangeDamage",
        SkillDescription = "RectRangeDamage",
        SkillSpellTime = 2.0f,
        SkillCooldownTime = 3.0f,
        GCDTime = 3.0f,
        NeedPosTarget = true,
        Damage = 200.0f,
        DamageType = "Physcial",
        Range = new RectRangeConfig {
            FarClamp = 5.0f,
        },
    };

    // ── Buffs ──

    public static BuffDOTConfig BuffDotMagic {
        get;
    } = new() {
        Id = "buff_dot_magic",
        BuffName = "持续魔法伤害",
        Duration = 30.0,
        MaxSuperpositions = 1,
        DamageType = "Magic",
        DamagePerSec = 10.0f,
    };

    public static BuffDOTConfig BuffDotPhyscial {
        get;
    } = new() {
        Id = "buff_dot_physcial",
        BuffName = "",
        Duration = 15.0,
        MaxSuperpositions = 1,
        DamageType = "Physcial",
        DamagePerSec = 100.0f,
    };

    public static BuffHOTConfig BuffHot {
        get;
    } = new() {
        Id = "buff_hot",
        BuffName = "持续治疗",
        Duration = 15.0,
        MaxSuperpositions = 1,
        HealthPerSec = 100.0f,
    };

    // ── 查找索引（懒加载，首次访问时构建）──

    private static Dictionary<string, SkillConfig>? _skills;
    private static Dictionary<string, BuffConfig>? _buffs;
    private static Dictionary<string, UnitConfig>? _units;

    private static void EnsureIndices() {
        if (_skills != null)
            return;

        _skills = new() {
            [SkillMagicDamage.Id] = SkillMagicDamage,
            [SkillCure.Id] = SkillCure,
            [SkillAddDotMagic.Id] = SkillAddDotMagic,
            [SkillAddHot.Id] = SkillAddHot,
            [SkillRectRangeDamage.Id] = SkillRectRangeDamage,
        };

        _buffs = new() {
            [BuffDotMagic.Id] = BuffDotMagic,
            [BuffDotPhyscial.Id] = BuffDotPhyscial,
            [BuffHot.Id] = BuffHot,
        };

        _units = [];
    }

    public static SkillConfig? GetSkill(string id) {
        EnsureIndices();
        return _skills!.TryGetValue(id, out var s) ? s : null;
    }

    public static BuffConfig? GetBuff(string id) {
        EnsureIndices();
        return _buffs!.TryGetValue(id, out var b) ? b : null;
    }

    public static UnitConfig? GetUnit(string id) {
        EnsureIndices();
        return _units!.TryGetValue(id, out var u) ? u : null;
    }

    public static IReadOnlyDictionary<string, SkillConfig> AllSkills {
        get {
            EnsureIndices();
            return _skills!;
        }
    }

    public static IReadOnlyDictionary<string, BuffConfig> AllBuffs {
        get {
            EnsureIndices();
            return _buffs!;
        }
    }

    // ── 配置 → 运行时 Model 转换 ──

    public static SkillModel ToSkillModel(SkillConfig config) {
        var model = config switch {
            SkillDamageConfig dmg => (SkillModel)new SkillDamageModel {
                Damage = dmg.Damage,
                DamageType = Enum.Parse<Enum_DamageType>(dmg.DamageType),
            },
            SkillCureConfig cure => new SkillCureModel {
                CurePotency = cure.CurePotency,
            },
            SkillAddBuffConfig addBuff => new SkillAddBuffModel {
                Buff = ToBuffModel(addBuff.BuffId),
            },
            SkillRangeDamageConfig rangeDmg => new SkillRangeDamageModel {
                Damage = rangeDmg.Damage,
                DamageType = Enum.Parse<Enum_DamageType>(rangeDmg.DamageType),
                RangeRes = ToRangeRes(rangeDmg.Range),
            },
            _ => new InternalSkillPlaceholder(),
        };

        model.SkillName = config.SkillName;
        model.SkillDescription = config.SkillDescription;
        model.SkillSpellTime = config.SkillSpellTime;
        model.SkillCooldownTime = config.SkillCooldownTime;
        model.GCDTime = config.GCDTime;
        model.NeedUnitTarget = config.NeedUnitTarget;
        model.NeedPosTarget = config.NeedPosTarget;
        model.SkillCanAdd = Enum.Parse<EnumSkillCanAdd>(config.SkillCanAdd);

        return model;
    }

    public static BuffModel ToBuffModel(BuffConfig config) {
        var model = config switch {
            BuffDOTConfig dot => (BuffModel)new BuffDOTModel {
                DamageType = Enum.Parse<Enum_DamageType>(dot.DamageType),
                DamagePerSec = dot.DamagePerSec,
            },
            BuffHOTConfig hot => new BuffHOTModel {
                HealthPerSec = hot.HealthPerSec,
            },
            _ => new BuffModel(),
        };

        model.BuffName = config.BuffName;
        model.BuffDescription = config.BuffDescription;
        model.Duration = config.Duration;
        model.MaxSuperpositions = config.MaxSuperpositions;

        return model;
    }

    private static BuffModel ToBuffModel(string buffId) {
        var config = GetBuff(buffId);
        return config != null ? ToBuffModel(config) : new BuffModel { BuffName = buffId };
    }

    private static IRangeRes ToRangeRes(RangeConfig config) {
        return config switch {
            CircularRangeConfig c => new CircularRangeRes {
                NearClamp = c.NearClamp,
                FarClamp = c.FarClamp,
                RadianFrom = c.RadianFrom,
                RadianTo = c.RadianTo,
            },
            RectRangeConfig r => new RectRangeRes {
                NearClamp = r.NearClamp,
                FarClamp = r.FarClamp,
                FromL = r.FromL,
                ToR = r.ToR,
            },
            _ => new CircularRangeRes(),
        };
    }

    private class InternalSkillPlaceholder : SkillModel {
        protected override void CallSpelledSkill() {
        }
    }
}
