using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Intelligence;

namespace DungeonChessBattle.GameConfig.Models;

/// <summary>
/// 单位配置，仅包含策划配表参数，不含运行时状态。
/// </summary>
public class UnitConfig {
    /// <summary>单位碰撞半径。</summary>
    public float BodyRadius { get; set; } = 1.0f;

    /// <summary>最大生命值。</summary>
    public float MaxHealth { get; set; } = 1000f;

    /// <summary>治疗强度系数即治疗倍率。</summary>
    public float CureIntensity { get; set; } = 1.0f;

    /// <summary>物理攻击基础系数即伤害倍率。</summary>
    public float PhysicalAttackBase { get; set; } = 1.0f;

    /// <summary>物理伤害承受系数即减免倍率。</summary>
    public float PhysicalTakePercent { get; set; } = 1.0f;

    /// <summary>魔法攻击基础系数即伤害倍率。</summary>
    public float MagicAttackBase { get; set; } = 1.0f;

    /// <summary>魔法伤害承受系数即减免倍率。</summary>
    public float MagicTakePercent { get; set; } = 1.0f;

    /// <summary>基础移动速度。</summary>
    public float BaseSpeed { get; set; } = 2.0f;

    /// <summary>单位归属阵营；玩家可选单位留空表示由玩家选择决定，敌人单位必填。</summary>
    public string? Camp {
        get; set;
    }

    /// <summary>单位拥有的技能定义列表。</summary>
    public SkillDefinition[] Skills { get; set; } = [];

    /// <summary>敌人单位智能，装配期直接引用领域行为实例，无状态实例可多单位共享；玩家可选单位不配。</summary>
    public IUnitIntelligence? Intelligence {
        get; set;
    }

    /// <summary>仇恨生成倍率，作用于该单位造成的伤害与治疗仇恨，默认 1.0。</summary>
    public float HateFactor { get; set; } = 1.0f;

    /// <summary>仇恨规则，以自身为中心评估事件产生仇恨；null 表示使用默认规则。</summary>
    public IHateRule? HateRule {
        get; set;
    }

    /// <summary>单位配置键，唯一身份标识，注册表与协议身份来源。</summary>
    public required string ConfigKey {
        get; init;
    }

    /// <summary>是否可被玩家在准备阶段选择，敌人单位设为 false。</summary>
    public bool IsPlayerSelectable { get; set; } = true;
}
