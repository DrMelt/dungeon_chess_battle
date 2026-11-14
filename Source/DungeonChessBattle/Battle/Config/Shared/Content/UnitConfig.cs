using DungeonChessBattle.Battle.Config.Shared.Combat;
using DungeonChessBattle.Battle.Config.Shared.Control;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Config.Shared.Content;

/// <summary>
/// 单位配置，仅包含策划配表参数，不含运行时状态。
/// </summary>
public sealed class UnitConfig {
    /// <summary>单位基础状态：不变基础数值，运行时实体直接引用本实例。</summary>
    public required UnitBaseConfig BaseConfig {
        get; init;
    }

    /// <summary>单位拥有的技能定义列表。</summary>
    public IReadOnlyList<SkillDefinition> Skills { get; init; } = [];

    /// <summary>自治驱动所用的控制者配置：装配期直接引用本层控制者实例；null 表示本单位不声明自治驱动。</summary>
    public UnitControllerConfig? Controller {
        get; init;
    }

    /// <summary>仇恨生成倍率，作用于该单位造成的伤害与治疗仇恨。</summary>
    public required float HateFactor {
        get; init;
    } = 1.0f;

    /// <summary>仇恨规则，以自身为中心评估事件产生仇恨；null 表示不参与仇恨计算。</summary>
    public IHateRule? HateRule {
        get; init;
    }

    /// <summary>单位配置键，唯一身份标识，注册表与协议身份来源。</summary>
    public required UnitConfigKey ConfigKey {
        get; init;
    }
}
