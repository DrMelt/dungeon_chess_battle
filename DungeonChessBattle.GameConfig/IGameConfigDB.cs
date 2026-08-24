using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.GameConfig.Models;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 游戏配置数据库的抽象接口，供消费者按契约引用，不依赖静态单例。
/// </summary>
public interface IGameConfigDB {
    /// <summary>魔法持续伤害 Buff 定义。</summary>
    DamageOverTimeBuff BuffDotMagic {
        get;
    }

    /// <summary>物理持续伤害 Buff 定义。</summary>
    DamageOverTimeBuff BuffDotPhysical {
        get;
    }

    /// <summary>持续治疗 Buff 定义。</summary>
    HealOverTimeBuff BuffHot {
        get;
    }

    /// <summary>魔法单体伤害技能定义。</summary>
    DamageSkillDefinition SkillMagicDamage {
        get;
    }

    /// <summary>治疗技能定义。</summary>
    HealSkillDefinition SkillCure {
        get;
    }

    /// <summary>添加魔法持续伤害 Buff 的技能定义。</summary>
    AddBuffSkillDefinition SkillAddDotMagic {
        get;
    }

    /// <summary>添加持续治疗 Buff 的技能定义。</summary>
    AddBuffSkillDefinition SkillAddHot {
        get;
    }

    /// <summary>矩形范围物理伤害技能定义。</summary>
    RangeDamageSkillDefinition SkillRectRangeDamage {
        get;
    }

    /// <summary>白法师单位配置。</summary>
    UnitConfig UnitWhiteMage {
        get;
    }
}
