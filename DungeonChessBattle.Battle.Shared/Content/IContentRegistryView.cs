using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Shared.Content;

/// <summary>
/// 内容注册表只读视图：按身份键查领域定义，不含任何注册或修订能力。
/// 展示面与只读消费方以此为判据源，实现对内容装配根零依赖。
/// </summary>
public interface IContentRegistryView {
    /// <summary>按技能键取定义；不存在返回 null。</summary>
    SkillDefinition? GetSkill(SkillKeyId skillKey);

    /// <summary>按 Buff 键取定义；不存在返回 null。</summary>
    BuffDefinition? GetBuff(BuffTypeId buffTypeId);

    /// <summary>按单位配置键取定义；不存在返回 null。</summary>
    UnitConfig? GetUnit(UnitConfigKey configKey);

    /// <summary>按副本键取定义；键为空或不存在返回 null。</summary>
    DungeonConfig? GetDungeon(string? dungeonKey);
}
