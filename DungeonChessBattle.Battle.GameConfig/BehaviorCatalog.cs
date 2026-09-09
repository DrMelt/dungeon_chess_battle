using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Intelligence;
using DungeonChessBattle.Battle.Mod.Shared;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 行为目录：行为 ID ↔ 无状态行为实例工厂，是 mod 注册面 <see cref="IModRuntime"/> 的实现。
/// 内容全部由 mod 提供，行为注册发生在 mod 代码入口装载时；本类只承担目录容器职责。
/// 行为实例必须无状态，可多单位、多房间共享。
/// </summary>
public sealed class BehaviorCatalog : IModRuntime {
    private readonly Dictionary<string, Func<ISkillEffect>> _skillEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IBuffEffect>> _buffEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IUnitIntelligence>> _intelligences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IHateRule>> _hateRules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CampRelationResolver> _campRelations = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public void RegisterSkillEffect(string id, Func<ISkillEffect> factory) => _skillEffects[id] = factory;

    /// <inheritdoc/>
    public void RegisterBuffEffect(string id, Func<IBuffEffect> factory) => _buffEffects[id] = factory;

    /// <inheritdoc/>
    public void RegisterIntelligence(string id, Func<IUnitIntelligence> factory) => _intelligences[id] = factory;

    /// <inheritdoc/>
    public void RegisterHateRule(string id, Func<IHateRule> factory) => _hateRules[id] = factory;

    /// <inheritdoc/>
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
