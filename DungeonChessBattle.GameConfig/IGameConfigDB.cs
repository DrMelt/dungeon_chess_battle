using DungeonChessBattle.Core.Models;
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

    /// <summary>
    /// 将单位配置转换为运行时单位模型。
    /// </summary>
    /// <param name="config">单位配置。</param>
    /// <returns>对应的 <see cref="UnitModel"/>。</returns>
    UnitModel ToUnitModel(UnitConfig config);

    /// <summary>
    /// 将技能配置转换为运行时技能模型。
    /// </summary>
    /// <param name="config">技能配置。</param>
    /// <returns>对应的 <see cref="SkillModel"/> 派生实例。</returns>
    SkillModel ToSkillModel(SkillConfig config);

    /// <summary>
    /// 将 Buff 配置转换为运行时 Buff 模型。
    /// </summary>
    /// <param name="config">Buff 配置。</param>
    /// <returns>对应的 <see cref="BuffModel"/> 派生实例。</returns>
    BuffModel ToBuffModel(BuffConfig config);
}
