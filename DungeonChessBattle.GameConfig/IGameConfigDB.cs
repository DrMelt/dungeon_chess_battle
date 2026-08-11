using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 游戏配置数据库的抽象接口，供消费者按契约引用，不依赖静态单例。
/// </summary>
public interface IGameConfigDB {
    /// <summary>魔法持续伤害 Buff 配置。</summary>
    BuffDOTConfig BuffDotMagic {
        get;
    }

    /// <summary>物理持续伤害 Buff 配置。</summary>
    BuffDOTConfig BuffDotPhysical {
        get;
    }

    /// <summary>持续治疗 Buff 配置。</summary>
    BuffHOTConfig BuffHot {
        get;
    }

    /// <summary>魔法单体伤害技能配置。</summary>
    SkillDamageConfig SkillMagicDamage {
        get;
    }

    /// <summary>治疗技能配置。</summary>
    SkillCureConfig SkillCure {
        get;
    }

    /// <summary>添加魔法持续伤害 Buff 的技能配置。</summary>
    SkillAddBuffConfig SkillAddDotMagic {
        get;
    }

    /// <summary>添加持续治疗 Buff 的技能配置。</summary>
    SkillAddBuffConfig SkillAddHot {
        get;
    }

    /// <summary>矩形范围物理伤害技能配置。</summary>
    SkillRangeDamageConfig SkillRectRangeDamage {
        get;
    }

    /// <summary>白法师单位配置。</summary>
    UnitConfig UnitWhiteMage {
        get;
    }
}
