using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Shared.Content;

/// <summary>
/// 内容注册表只读视图：按身份键查领域定义，并暴露内容修订号，不含任何注册能力。
/// 展示面与只读消费方以此为判据源，实现对内容装配根零依赖。
/// </summary>
public interface IContentRegistryView {
    /// <summary>内容修订号：引擎内容修订号与装配方内容指纹合成，内容与布局任何变化都会改变值。</summary>
    string DataRevision {
        get;
    }

    /// <summary>按技能键取定义；不存在返回 null。</summary>
    SkillDefinition? GetSkill(SkillKeyId skillKey);

    /// <summary>按 Buff 键取定义；不存在返回 null。</summary>
    BuffDefinition? GetBuff(BuffTypeId buffTypeId);

    /// <summary>按单位配置键取定义；不存在返回 null。</summary>
    UnitConfig? GetUnit(UnitConfigKey configKey);

    /// <summary>按副本键取定义；无键或不存在返回 null。</summary>
    DungeonConfig? GetDungeon(DungeonKeyId dungeonKey);
}
