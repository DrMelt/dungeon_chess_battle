using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Shared.Display;

namespace DungeonChessBattle.Game.Display.Registry;

/// <summary>
/// 展示数据读面：四类条目展示数据按内容键读，写经 <see cref="IDisplayRegistrar"/>。
/// 条目数据同键后注册者覆盖先注册者，注册次序由装配方保证；装配期读到的内容取决于已完成的注册。
/// 场景模板与图标纹理都随条目数据以对象直接携带，不经本口登记或解析，mod 引自己包内的
/// <c>res://mods/{mod id}/</c> 资源即可，无须全局名。
/// </summary>
public interface IDisplayRegistry {
    /// <summary>按技能键取展示数据；未注册返回 null。</summary>
    SkillDisplay? GetSkill(SkillKeyId skillId);

    /// <summary>按 Buff 键取展示数据；未注册返回 null。</summary>
    BuffDisplay? GetBuff(BuffTypeId buffTypeId);

    /// <summary>按副本键取展示数据；无键或未注册返回 null。</summary>
    DungeonDisplay? GetDungeon(DungeonKeyId dungeonKey);

    /// <summary>按单位配置键取展示数据；未注册返回 null。</summary>
    UnitDisplay? GetUnit(UnitConfigKey configKey);
}
