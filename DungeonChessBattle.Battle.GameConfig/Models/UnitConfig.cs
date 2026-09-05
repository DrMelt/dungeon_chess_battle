using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Intelligence;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.GameConfig.Models;

/// <summary>
/// 单位配置，仅包含策划配表参数，不含运行时状态。
/// </summary>
public class UnitConfig {
    /// <summary>单位基础状态：不变基础数值，运行时实体直接引用本实例。</summary>
    public required UnitBaseConfig BaseConfig {
        get; init;
    }

    /// <summary>单位拥有的技能定义列表。</summary>
    public IReadOnlyList<SkillDefinition> Skills { get; set; } = [];

    /// <summary>敌人单位智能，装配期直接引用本层决策实现实例，无状态实例可多单位共享；玩家可选单位不配。</summary>
    public IUnitIntelligence? Intelligence {
        get; set;
    }

    /// <summary>仇恨生成倍率，作用于该单位造成的伤害与治疗仇恨，默认 1.0。</summary>
    public float HateFactor { get; set; } = 1.0f;

    /// <summary>仇恨规则，以自身为中心评估事件产生仇恨；null 表示不参与仇恨计算。</summary>
    public IHateRule? HateRule {
        get; set;
    }

    /// <summary>单位配置键，唯一身份标识，注册表与协议身份来源。</summary>
    public required UnitConfigKey ConfigKey {
        get; init;
    }

    /// <summary>是否可被玩家在准备阶段选择，敌人单位设为 false。</summary>
    public bool IsPlayerSelectable { get; set; } = true;
}
