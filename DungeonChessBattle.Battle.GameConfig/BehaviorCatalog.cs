using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using CampRelationEnum = DungeonChessBattle.Battle.Shared.Enums.CampRelation;
using DungeonChessBattle.Battle.Shared.Intelligence;
using DungeonChessBattle.Battle.GameConfig.Buffs;
using DungeonChessBattle.Battle.GameConfig.Intelligence;
using DungeonChessBattle.Battle.GameConfig.Skills;
using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 行为目录：行为 ID ↔ 无状态行为实例工厂，是 content.json 行为字段（effect / ai / hateRule / relations）的唯一解析口。
/// 内置行为先注册，mod 代码程序集后注册可覆盖同名 ID。行为实例必须无状态，可多单位、多房间共享。
/// </summary>
public sealed class BehaviorCatalog : IModRuntime {
    private readonly Dictionary<string, Func<ISkillEffect>> _skillEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IBuffEffect>> _buffEffects = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IUnitIntelligence>> _intelligences = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Func<IHateRule>> _hateRules = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CampRelationResolver> _campRelations = new(StringComparer.Ordinal);

    /// <summary>创建并注册全部内置行为。</summary>
    public BehaviorCatalog() => RegisterBuiltIn();

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

    private void RegisterBuiltIn() {
        RegisterSkillEffect(BehaviorIds.SkillEffect.Damage, static () => new DamageEffect());
        RegisterSkillEffect(BehaviorIds.SkillEffect.Heal, static () => new HealEffect());
        RegisterSkillEffect(BehaviorIds.SkillEffect.AddBuff, static () => new AddBuffEffect());
        RegisterSkillEffect(BehaviorIds.SkillEffect.Hate, static () => new HateSkillEffect());
        RegisterSkillEffect(BehaviorIds.SkillEffect.RangeDamage, static () => new RangeDamageEffect());

        RegisterBuffEffect(BehaviorIds.BuffEffect.Dot, static () => new DotEffect());
        RegisterBuffEffect(BehaviorIds.BuffEffect.Hot, static () => new HotEffect());

        RegisterIntelligence(BehaviorIds.Intelligence.EnemyBasic, static () => new EnemyIntelligence());

        RegisterHateRule(BehaviorIds.HateRule.Default, static () => new DefaultHateRule());

        RegisterCampRelation(BehaviorIds.CampRelation.PveBoss, CampRelationsPve);
    }

    /// <summary>PvE 阵营关系：双方均含 Boss 阵营为友；任一方含 Boss 阵营即敌对；存在共同阵营为友；其余返回 Unknown。</summary>
    private static CampRelationEnum CampRelationsPve(
        IReadOnlyList<string> sourceCamps, IReadOnlyList<string> targetCamps) {
        bool sourceHasBoss = sourceCamps.Contains(CampConstants.CampBoss);
        bool targetHasBoss = targetCamps.Contains(CampConstants.CampBoss);

        if (sourceHasBoss && targetHasBoss)
            return CampRelationEnum.Friendly;
        if (sourceHasBoss || targetHasBoss)
            return CampRelationEnum.Enemy;

        if (sourceCamps.Any(camp => targetCamps.Contains(camp)))
            return CampRelationEnum.Friendly;
        return CampRelationEnum.Unknown;
    }
}
