using System.Numerics;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat.Hates;
using DungeonChessBattle.Battle.Shared.Intelligence;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗单位领域实体：战斗世界自持的全部权威状态。
/// 属性与技能装配期写入，战斗状态由 BattleScene 推进。
/// 只读消费经 <see cref="IBattleUnitView"/> 收窄能力；不依赖任何网络与框架类型，服务端、在线与回放共用。
/// 展示层经 <see cref="IUnitUiView"/> 收窄展示能力，Buff 经 <see cref="IBuffUiView"/> 供展示层读取。
/// 在线端本实体是下行回填容器：动态状态与 <see cref="RuntimeState"/> 由 <c>UnitPawn.SyncInto</c> 覆写，
/// 基础数值经 <see cref="BaseConfig"/> 读取单位基础状态，不参与结算。
/// </summary>
public sealed class BattleUnit : IBattleUnitView, IUnitUiView {
    /// <summary>单位基础状态，基础数值与基准技能集的唯一来源，装配期注入后不变。</summary>
    public required UnitBaseConfig BaseConfig {
        get; init;
    }

    /// <inheritdoc />
    public required UnitId UnitId {
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

    /// <summary>仇恨规则，装配期直传配置；null 表示不参与仇恨计算。</summary>
    public IHateRule? HateRule {
        get; init;
    }

    /// <inheritdoc />
    IHateRule? IHateActorView.HateRule => HateRule;

    /// <inheritdoc />
    public float HateFactor {
        get; init;
    } = 1f;

    /// <summary>最大生命值，取自配置基础值。</summary>
    public float MaxHealth => BaseConfig.MaxHealth;

    /// <summary>当前生命值。</summary>
    public float Health {
        get; set;
    }

    /// <summary>单位是否已死亡：当前生命值 ≤ 0。死亡判据唯一来源，领域结算、投影校正与展示隐藏统一依此。</summary>
    public bool IsDead => Health <= 0f;

    /// <summary>物理攻击基础系数即伤害倍率，取自配置基础值。</summary>
    public float PhysicalAttackBase => BaseConfig.PhysicalAttackBase;

    /// <summary>物理伤害承受系数即减免倍率，取自配置基础值。</summary>
    public float PhysicalTakePercent => BaseConfig.PhysicalTakePercent;

    /// <summary>魔法攻击基础系数即伤害倍率，取自配置基础值。</summary>
    public float MagicAttackBase => BaseConfig.MagicAttackBase;

    /// <summary>魔法伤害承受系数即减免倍率，取自配置基础值。</summary>
    public float MagicTakePercent => BaseConfig.MagicTakePercent;

    /// <summary>治疗强度系数即治疗倍率，取自配置基础值。</summary>
    public float CureIntensity => BaseConfig.CureIntensity;

    /// <summary>基础移动速度，取自配置基础值。</summary>
    public float BaseSpeed => BaseConfig.BaseSpeed;

    /// <summary>碰撞半径，取自配置基础值。</summary>
    public float BodyRadius => BaseConfig.BodyRadius;

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

    /// <summary>服务端权威战斗状态：读条目标、Buff、冷却、仇恨权威在此；在线端 Buff 与冷却为下行回填的展示壳。</summary>
    public UnitCombatState RuntimeState { get; } = new();

    /// <summary>当前世界位置，XZ 平面。</summary>
    public Vector2 Position {
        get; set;
    }

    /// <summary>当前朝向方向向量，XZ 平面。</summary>
    public Vector2 Direction {
        get; set;
    }

    /// <summary>
    /// 本帧移动意图，写者全在 Battle.Logic，<c>BattleScene.Tick</c> 末作废。零值即静止。
    /// 本帧两个读者：位移解算取方向，读条推进据其非零与否判打断。
    /// 非同步字段，不进 <c>UnitPawn.SyncFrom/SyncInto</c> 清单，在线端恒零。
    /// </summary>
    public Vector2 MoveInput {
        get; internal set;
    }

    /// <summary>
    /// 本帧施法意图，null 表示无。写者全在 Battle.Logic，由 <c>BattleScene.Tick</c> 的读条推进段消费、末尾作废；
    /// 一帧一份，后写覆盖先写。非同步字段，裁定通过后才转为读条状态并投影。
    /// </summary>
    public CastIntent? CastInput {
        get; internal set;
    }

    /// <summary>
    /// 聚焦目标，<see cref="UnitId.None"/> 表示无。持续展示态，非逐帧意图：由输入门面设定后保持，
    /// 目标死亡或消失随 <c>BattleScene.Tick</c> 清零。只影响展示，不参与结算，在线端随同步通道回填。
    /// </summary>
    public UnitId FocusTarget {
        get; set;
    }

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
    public float HateOf(UnitId targetUnitId) => RuntimeState.Hates.ValueOf(targetUnitId);

    /// <inheritdoc />
    public IReadOnlyList<CooldownEntry> Cooldowns => RuntimeState.Cooldowns;

    /// <inheritdoc />
    public IReadOnlyList<ActiveBuff> Buffs => RuntimeState.Buffs;

    /// <inheritdoc />
    IReadOnlyList<IBuffUiView> IUnitUiView.Buffs => RuntimeState.Buffs;
}
