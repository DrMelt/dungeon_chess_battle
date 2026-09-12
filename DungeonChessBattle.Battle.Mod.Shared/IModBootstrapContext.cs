using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;

namespace DungeonChessBattle.Battle.Mod.Shared;

/// <summary>
/// mod 引导上下文：数据入口初始化时拿到的唯一句柄，只做内容定义注册。
/// 内容按领域对象直接注册，定义对象是运行时强类型，非字符串 schema——mod 必先构造对象图再注册；
/// 行为实现随内容就地构造并注入定义，不经 ID 查表。
/// 同键后写覆盖；Buff 以 <see cref="BuffDefinition.BuffTypeId"/> 为同步身份。
/// </summary>
public interface IModBootstrapContext {
    /// <summary>注册技能定义，同 SkillId 覆盖。</summary>
    void RegisterSkill(SkillDefinition skill);

    /// <summary>注册 Buff 定义，同 BuffTypeId 覆盖。</summary>
    void RegisterBuff(BuffDefinition buff);

    /// <summary>注册单位配置，同 ConfigKey 覆盖。</summary>
    void RegisterUnit(UnitConfig unit);

    /// <summary>注册副本配置，同 DungeonKey 覆盖，空键拒绝。</summary>
    void RegisterDungeon(DungeonConfig dungeon);
}

