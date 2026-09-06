using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Intelligence;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 运行期注册面：行为按字符串 ID 注册进行为目录，内容按领域对象直接注册。
/// 注册什么由内容方定，宿主只递注册器；同一 ID 重复注册以后注册者覆盖先注册者。
/// 行为实现必须无状态，可被任意多个单位与房间共享。
/// </summary>
public interface IModRuntime {
    /// <summary>注册技能效果实现。</summary>
    void RegisterSkillEffect(string id, Func<ISkillEffect> factory);

    /// <summary>注册 Buff 持续效果实现。</summary>
    void RegisterBuffEffect(string id, Func<IBuffEffect> factory);

    /// <summary>注册敌人智能决策实现。</summary>
    void RegisterIntelligence(string id, Func<IUnitIntelligence> factory);

    /// <summary>注册仇恨规则实现。</summary>
    void RegisterHateRule(string id, Func<IHateRule> factory);

    /// <summary>注册阵营关系函数。</summary>
    void RegisterCampRelation(string id, CampRelationResolver resolver);

    /// <summary>取技能效果行为实例，供构造技能定义填入 Effect；未知 ID 抛异常。</summary>
    ISkillEffect SkillEffect(string id);

    /// <summary>取 Buff 持续效果行为实例，供构造 Buff 定义填入 Effect；未知 ID 抛异常。</summary>
    IBuffEffect BuffEffect(string id);

    /// <summary>取敌人决策行为实例，供构造单位配置填入 Intelligence；未知 ID 抛异常。</summary>
    IUnitIntelligence Intelligence(string id);

    /// <summary>取仇恨规则行为实例，供构造单位配置填入 HateRule；未知 ID 抛异常。</summary>
    IHateRule HateRule(string id);

    /// <summary>取阵营关系函数，供构造副本配置填入 RelationsResolver；未知 ID 抛异常。</summary>
    CampRelationResolver CampRelation(string id);
}

/// <summary>
/// mod 内容注册面：把领域定义对象（技能/Buff/单位/副本）直接注册进内容注册表。
/// 定义对象是运行时强类型，非字符串 schema——mod 必先构造对象图再注册。
/// 同键后写覆盖；Buff 以 <see cref="BuffDefinition.BuffTypeId"/> 为同步身份，冲突即异常。
/// </summary>
public interface IModContentRuntime {
    /// <summary>注册技能定义，同 SkillId 覆盖。</summary>
    void RegisterSkill(SkillDefinition skill);

    /// <summary>注册 Buff 定义，同 BuffTypeId 覆盖或冲突校验；越引擎段（>999）由宿主校验。</summary>
    void RegisterBuff(BuffDefinition buff);

    /// <summary>注册单位配置，同 ConfigKey 覆盖。</summary>
    void RegisterUnit(UnitConfig unit);

    /// <summary>注册副本配置，同 DungeonKey 覆盖。</summary>
    void RegisterDungeon(DungeonConfig dungeon);

    /// <summary>覆盖默认副本键；未声明沿用基座。</summary>
    void SetDefaultDungeonKey(string key);
}

/// <summary>
/// mod 引导上下文：行为注册与内容注册的合成句柄，<see cref="IModEntry.Initialize"/> 唯一参数。
/// 行为实现只依赖 Battle.Shared 契约，内容定义对象经本接口交给内容注册表。
/// </summary>
public interface IModBootstrapContext : IModRuntime, IModContentRuntime;
