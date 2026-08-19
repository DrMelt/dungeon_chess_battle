using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Intelligence;

namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 编排层读写具体战斗单位的通道，依赖倒置，由 Entities 的 UnitPawn 实现。
/// Logic 层面向本接口编排结算，不依赖任何网络/框架类型。
/// 读条为 Logic 每帧推进的权威状态，经本接口写回载体同步；冷却与 Buff 以截止 tick 在起始与结构变化时投影，剩余由服务端与客户端按本端 tick 本地推算，载体仅承担存储与映射。
/// </summary>
public interface IBattleUnit {
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

    /// <summary>当前战斗结算快照，只读输入。</summary>
    UnitSnapshot Snapshot {
        get;
    }

    /// <summary>当前生命值。</summary>
    float Health {
        get; set;
    }

    /// <summary>最大生命值。</summary>
    float MaxHealth {
        get;
    }

    /// <summary>当前施法技能，default 表示无施法。</summary>
    SkillKeyId SkillCasting {
        get; set;
    }

    /// <summary>当前施法剩余读条时间，秒。</summary>
    float SkillCastRemaining {
        get; set;
    }

    /// <summary>剩余全局冷却时间，秒。</summary>
    float GcdRemaining {
        get; set;
    }

    /// <summary>写入单个技能的个体冷却，剩余秒数。</summary>
    void SetSkillCooldown(SkillKeyId skillKey, float remaining);

    /// <summary>单位是否拥有该技能，服务端权威归属判定。</summary>
    bool HasSkill(SkillKeyId skillKey);

    /// <summary>单位装备的全部技能定义，装配期写入后只读；AI 决策按配置顺序枚举。</summary>
    IReadOnlyList<SkillDefinition> Skills {
        get;
    }

    /// <summary>按技能 ID 获取技能定义，单位未装备该技能时返回 null。</summary>
    SkillDefinition? GetSkill(SkillKeyId skillKey);

    /// <summary>读取单个技能的总冷却剩余秒数（全局冷却与个体冷却取较大者），无冷却时返回 0。</summary>
    float GetTotalCooldownRemaining(SkillKeyId skillKey);

    /// <summary>当前 Buff 快照，展示投影。</summary>
    IReadOnlyList<BuffView> Buffs {
        get;
    }

    /// <summary>以全量投影方式同步 Buff 列表，服务端权威。</summary>
    void ReplaceBuffs(IReadOnlyList<BuffView> buffs);

    /// <summary>仇恨生成倍率，作用于该单位造成的伤害与治疗仇恨。</summary>
    float HateFactor {
        get;
    }

    /// <summary>仇恨规则，以自身为中心评估事件产生仇恨；装配期由载体写入，不参与网络同步。</summary>
    IHateRule HateRule {
        get;
    }

    /// <summary>当前仇恨快照，服务端权威投影；查询者只读，不含写通道。</summary>
    IReadOnlyList<HateSnapshot> Hates {
        get;
    }

    /// <summary>以全量投影方式同步仇恨列表，服务端权威。</summary>
    void ReplaceHates(IReadOnlyList<HateSnapshot> hates);

    /// <summary>单位智能决策器，null 表示由外部输入驱动（玩家单位）。装配期写入后只读，AI 驱动识别依据。</summary>
    IUnitIntelligence? Intelligence {
        get;
    }

    /// <summary>写入本帧移动输入，由实体确定性移动结算消费。AI 决策与外部输入共用。</summary>
    void SetMovementInput(Vector2 moveDirection);

    /// <summary>单位服务端权威战斗状态，载体持有；读条目标、Buff、冷却权威在此，同步经既有投影通道。</summary>
    UnitCombatState RuntimeState {
        get;
    }
}
