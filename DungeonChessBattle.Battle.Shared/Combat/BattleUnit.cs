using System.Numerics;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Intelligence;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗单位领域实体：战斗世界自持的全部权威状态。
/// 属性与技能装配期写入，战斗状态由 BattleScene 推进，移动输入由 BattleScene 结算。
/// 只读消费经 <see cref="IBattleUnitView"/> 收窄能力；不依赖任何网络与框架类型，服务端与回放共用。
/// 展示层经 <see cref="IUnitUiView"/> 收窄展示能力，Buff 经 <see cref="IBuffUiView"/> 供展示层读取。
/// </summary>
public sealed class BattleUnit : IBattleUnitView, IProjectableBattleState, IUnitUiView {
    /// <inheritdoc />
    public required ushort UnitNetId {
        get; init;
    }

    /// <inheritdoc />
    public required string UnitName {
        get; init;
    }

    /// <inheritdoc />
    public required IReadOnlyList<string> Camps {
        get; init;
    }

    /// <inheritdoc />
    public required IReadOnlyList<SkillDefinition> Skills {
        get; init;
    }

    /// <summary>单位智能决策器，敌人单位装配，玩家单位为空。</summary>
    public IUnitIntelligence? Intelligence {
        get; init;
    }

    /// <summary>仇恨规则，未装配时经 <see cref="EffectiveHateRule"/> 用默认规则。</summary>
    public IHateRule? HateRule {
        get; init;
    }

    /// <inheritdoc />
    IHateRule IHateActorView.HateRule => EffectiveHateRule;

    /// <inheritdoc />
    public float HateFactor {
        get; init;
    } = 1f;

    /// <summary>最大生命值，装配时写入。</summary>
    public float MaxHealth {
        get; set;
    }

    /// <summary>当前生命值。</summary>
    public float Health {
        get; set;
    }

    /// <summary>单位是否已死亡：当前生命值 ≤ 0。领域权威死亡判定，仅服务端与回放结算使用。</summary>
    public bool IsDead => Health <= 0f;

    /// <summary>物理攻击基础系数即伤害倍率。</summary>
    public float PhysicalAttackBase {
        get; set;
    } = 1f;

    /// <summary>物理伤害承受系数即减免倍率。</summary>
    public float PhysicalTakePercent {
        get; set;
    } = 1f;

    /// <summary>魔法攻击基础系数即伤害倍率。</summary>
    public float MagicAttackBase {
        get; set;
    } = 1f;

    /// <summary>魔法伤害承受系数即减免倍率。</summary>
    public float MagicTakePercent {
        get; set;
    } = 1f;

    /// <summary>治疗强度系数即治疗倍率。</summary>
    public float CureIntensity {
        get; set;
    } = 1f;

    /// <summary>基础移动速度。</summary>
    public float BaseSpeed {
        get; set;
    }

    /// <summary>碰撞半径，供技能范围判定与空间互斥使用。</summary>
    public float BodyRadius {
        get; set;
    }

    /// <summary>当前施法技能，default 表示无施法。</summary>
    public SkillKeyId SkillCasting {
        get; set;
    }

    /// <summary>当前施法剩余读条时间，秒。</summary>
    public float SkillCastRemaining {
        get; set;
    }

    /// <summary>全局冷却剩余时间，秒。</summary>
    public float GcdRemaining {
        get; set;
    }

    /// <summary>服务端权威战斗状态：读条目标、Buff、冷却、仇恨权威在此。</summary>
    public UnitCombatState RuntimeState { get; } = new();

    /// <summary>当前世界位置，XZ 平面。</summary>
    public Vector2 Position {
        get; set;
    }

    /// <summary>当前朝向方向向量，XZ 平面。</summary>
    public Vector2 Direction {
        get; set;
    }

    /// <summary>本帧移动输入，由 AI 决策或玩家输入写入，领域 BattleScene 结算。</summary>
    public Vector2 MoveInput {
        get; set;
    }

    /// <summary>仇恨规则，未装配时用默认规则。</summary>
    public IHateRule EffectiveHateRule => HateRule ?? DefaultHateRule.Instance;

    /// <inheritdoc />
    public UnitSnapshot Snapshot => new() {
        Health = Health,
        MaxHealth = MaxHealth,
        PhysicalAttackBase = PhysicalAttackBase,
        PhysicalTakePercent = PhysicalTakePercent,
        MagicAttackBase = MagicAttackBase,
        MagicTakePercent = MagicTakePercent,
        CureIntensity = CureIntensity,
        MoveSpeed = BaseSpeed,
        Position = Position,
        BodyRadius = BodyRadius,
    };

    /// <inheritdoc />
    public bool HasSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    public SkillDefinition? GetSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return skill;
        }
        return null;
    }

    /// <inheritdoc />
    public float GetTotalCooldownRemaining(SkillKeyId skillKey) {
        float remaining = GcdRemaining;
        foreach (var cd in RuntimeState.Cooldowns) {
            if (cd.SkillKey != skillKey)
                continue;
            return MathF.Max(remaining, cd.Remaining);
        }
        return remaining;
    }

    /// <inheritdoc />
    public IReadOnlyList<HateSnapshot> Hates => RuntimeState.Hates.Snapshot();

    /// <inheritdoc />
    public IReadOnlyList<CooldownEntry> Cooldowns => RuntimeState.Cooldowns;

    /// <inheritdoc />
    public IReadOnlyList<ActiveBuff> Buffs => RuntimeState.Buffs;

    /// <inheritdoc />
    IReadOnlyList<IBuffUiView> IUnitUiView.Buffs => RuntimeState.Buffs;
}
