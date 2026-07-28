using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 游戏配置数据库的抽象接口，供消费者按契约引用，不依赖静态单例。
/// </summary>
public interface IGameConfigDB {
    BuffDOTConfig BuffDotMagic {
        get;
    }
    BuffDOTConfig BuffDotPhyscial {
        get;
    }
    BuffHOTConfig BuffHot {
        get;
    }
    SkillDamageConfig SkillMagicDamage {
        get;
    }
    SkillCureConfig SkillCure {
        get;
    }
    SkillAddBuffConfig SkillAddDotMagic {
        get;
    }
    SkillAddBuffConfig SkillAddHot {
        get;
    }
    SkillRangeDamageConfig SkillRectRangeDamage {
        get;
    }
    UnitConfig UnitWhiteMage {
        get;
    }

    UnitModel ToUnitModel(UnitConfig config);
    SkillModel ToSkillModel(SkillConfig config);
    BuffModel ToBuffModel(BuffConfig config);
}
