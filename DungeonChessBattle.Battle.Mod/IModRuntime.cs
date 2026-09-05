using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Intelligence;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 运行期注册面：把行为实现按字符串 ID 注册进行为目录，供内容数据（content.json 的 effect/ai/hateRule/relations 字段）
/// 引用。行为实现必须无状态，可被任意多个单位与房间共享。
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
}
