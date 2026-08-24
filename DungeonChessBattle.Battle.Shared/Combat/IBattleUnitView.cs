using DungeonChessBattle.Battle.Shared.Combat.Hates;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>单位身份与阵营只读视图，敌我判定与展示用。</summary>
public interface IUnitIdentityView {
    /// <summary>单位名称，供调试与展示使用。</summary>
    string UnitName {
        get;
    }

    /// <summary>单位网络实体 ID，房间内唯一，领域事件标识用。</summary>
    ushort UnitNetId {
        get;
    }

    /// <summary>单位所属阵营列表。</summary>
    IReadOnlyList<string> Camps {
        get;
    }
}

/// <summary>战斗数值与读条状态只读视图，结算与施法校验用。</summary>
public interface ICombatStatsView {
    /// <summary>当前战斗结算快照，只读输入。</summary>
    UnitSnapshot Snapshot {
        get;
    }

    /// <summary>当前生命值。</summary>
    float Health {
        get;
    }

    /// <summary>当前施法技能，default 表示无施法。</summary>
    SkillKeyId SkillCasting {
        get;
    }
}

/// <summary>技能来源只读视图：技能集与冷却查询。</summary>
public interface ISkillSource {
    /// <summary>单位是否拥有该技能。</summary>
    bool HasSkill(SkillKeyId skillKey);

    /// <summary>单位装备的全部技能定义，AI 决策按配置顺序枚举。</summary>
    IReadOnlyList<SkillDefinition> Skills {
        get;
    }

    /// <summary>按技能 ID 获取技能定义，单位未装备该技能时返回 null。</summary>
    SkillDefinition? GetSkill(SkillKeyId skillKey);

    /// <summary>读取单个技能的总冷却剩余秒数（全局冷却与个体冷却取较大者），无冷却时返回 0。</summary>
    float GetTotalCooldownRemaining(SkillKeyId skillKey);
}

/// <summary>仇恨消费只读视图：仇恨快照、生成倍率与规则。</summary>
public interface IHateActorView {
    /// <summary>当前仇恨快照，AI 目标选择只读输入。</summary>
    IReadOnlyList<HateSnapshot> Hates {
        get;
    }

    /// <summary>仇恨生成倍率，作用于该单位造成的伤害与治疗仇恨。</summary>
    float HateFactor {
        get;
    }

    /// <summary>仇恨规则，以自身为中心评估事件产生仇恨。</summary>
    IHateRule HateRule {
        get;
    }
}

/// <summary>
/// 战斗单位只读视图：AI 决策、施法校验与仇恨规则的只读消费入口。
/// 按角色拆分为 <see cref="IUnitIdentityView"/>、<see cref="ICombatStatsView"/>、
/// <see cref="ISkillSource"/>、<see cref="IHateActorView"/>，各消费者按需依赖最小子集；
/// 读写能力只保留在 <see cref="BattleUnit"/> 具体类，本接口不暴露任何写通道。
/// </summary>
public interface IBattleUnitView : IUnitIdentityView, ICombatStatsView, ISkillSource, IHateActorView {
}
