using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Intelligence;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 行为目录：行为 ID ↔ 无状态行为实例工厂。引擎不内置行为，注册发生在内容装配期，
/// 本类只做容器，注册面适配与写入次序在装配层。行为实例必须无状态，可多单位、多房间共享。
/// </summary>
public sealed class BehaviorCatalog {
    private readonly Dictionary<string, Func<ISkillEffect>> _skillEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IBuffEffect>> _buffEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IUnitIntelligence>> _intelligences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IHateRule>> _hateRules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CampRelationResolver> _campRelations = new(StringComparer.Ordinal);

    /// <summary>注册技能效果工厂；同 ID 后注册覆盖。</summary>
    public void RegisterSkillEffect(string id, Func<ISkillEffect> factory) => _skillEffects[id] = factory;

    /// <summary>注册 Buff 持续效果工厂；同 ID 后注册覆盖。</summary>
    public void RegisterBuffEffect(string id, Func<IBuffEffect> factory) => _buffEffects[id] = factory;

    /// <summary>注册敌人智能决策工厂；同 ID 后注册覆盖。</summary>
    public void RegisterIntelligence(string id, Func<IUnitIntelligence> factory) => _intelligences[id] = factory;

    /// <summary>注册仇恨规则工厂；同 ID 后注册覆盖。</summary>
    public void RegisterHateRule(string id, Func<IHateRule> factory) => _hateRules[id] = factory;

    /// <summary>注册阵营关系函数；同 ID 后注册覆盖。</summary>
    public void RegisterCampRelation(string id, CampRelationResolver resolver) => _campRelations[id] = resolver;

    /// <summary>按 ID 取技能效果实现；未知 ID 抛异常，杜绝静默回退。</summary>
    public ISkillEffect SkillEffect(string id) => Require(_skillEffects, id)();

    /// <summary>按 ID 取 Buff 持续效果实现；未知 ID 抛异常。</summary>
    public IBuffEffect BuffEffect(string id) => Require(_buffEffects, id)();

    /// <summary>按 ID 取敌人决策实现；未知 ID 抛异常。</summary>
    public IUnitIntelligence Intelligence(string id) => Require(_intelligences, id)();

    /// <summary>按 ID 取仇恨规则实现；未知 ID 抛异常。</summary>
    public IHateRule HateRule(string id) => Require(_hateRules, id)();

    /// <summary>按 ID 取阵营关系函数；未知 ID 抛异常。</summary>
    public CampRelationResolver CampRelation(string id) {
        if (_campRelations.TryGetValue(id, out var relation))
            return relation;
        throw new InvalidOperationException($"未注册阵营关系行为: {id}");
    }

    private static T Require<T>(IReadOnlyDictionary<string, T> table, string id) {
        if (table.TryGetValue(id, out var value))
            return value;
        throw new InvalidOperationException($"未注册行为: {id}");
    }
}
