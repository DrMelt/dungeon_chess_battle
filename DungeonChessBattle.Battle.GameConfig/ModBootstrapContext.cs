using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Content;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Intelligence;
using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// mod 引导上下文实现：行为注册与取用转发 <see cref="BehaviorCatalog"/>，内容注册转发 <see cref="ContentSetRegistry"/>。
/// mod 数据代码入口经本上下文既登记行为又把领域定义对象写进注册表，内置内容先注册故 mod 后注册可覆盖。
/// </summary>
public sealed class ModBootstrapContext(BehaviorCatalog behaviors, ContentSetRegistry registry) : IModBootstrapContext {
    /// <inheritdoc/>
    public void RegisterSkillEffect(string id, Func<ISkillEffect> factory) =>
        behaviors.RegisterSkillEffect(id, factory);

    /// <inheritdoc/>
    public void RegisterBuffEffect(string id, Func<IBuffEffect> factory) =>
        behaviors.RegisterBuffEffect(id, factory);

    /// <inheritdoc/>
    public void RegisterIntelligence(string id, Func<IUnitIntelligence> factory) =>
        behaviors.RegisterIntelligence(id, factory);

    /// <inheritdoc/>
    public void RegisterHateRule(string id, Func<IHateRule> factory) =>
        behaviors.RegisterHateRule(id, factory);

    /// <inheritdoc/>
    public void RegisterCampRelation(string id, CampRelationResolver resolver) =>
        behaviors.RegisterCampRelation(id, resolver);

    /// <inheritdoc/>
    public ISkillEffect SkillEffect(string id) => behaviors.SkillEffect(id);

    /// <inheritdoc/>
    public IBuffEffect BuffEffect(string id) => behaviors.BuffEffect(id);

    /// <inheritdoc/>
    public IUnitIntelligence Intelligence(string id) => behaviors.Intelligence(id);

    /// <inheritdoc/>
    public IHateRule HateRule(string id) => behaviors.HateRule(id);

    /// <inheritdoc/>
    public CampRelationResolver CampRelation(string id) => behaviors.CampRelation(id);

    /// <inheritdoc/>
    public void RegisterSkill(SkillDefinition skill) => registry.RegisterSkill(skill);

    /// <inheritdoc/>
    public void RegisterBuff(BuffDefinition buff) => registry.RegisterModBuff(buff);

    /// <inheritdoc/>
    public void RegisterUnit(UnitConfig unit) => registry.RegisterUnit(unit);

    /// <inheritdoc/>
    public void RegisterDungeon(DungeonConfig dungeon) => registry.RegisterDungeon(dungeon);

    /// <inheritdoc/>
    public void SetDefaultDungeonKey(string key) => registry.SetDefaultDungeonKey(key);
}
