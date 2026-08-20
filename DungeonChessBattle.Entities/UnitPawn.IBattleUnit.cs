using System.Numerics;
using DungeonChessBattle.Battle.Domain;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Battle.Domain.Enums;
using DungeonChessBattle.Battle.Domain.Intelligence;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

// UnitPawn 对 IBattleUnit 接口的适配：把 LES SyncVar/SyncList 映射为领域读写通道。
// 领域结算 BattleScene 面向 IBattleUnit，不感知网络载体；本文件仅做值映射与 AI 决策执行闭环，无结算逻辑。
// 倒计时换算（GCD/冷却/Buff）依赖 EntityManager 服务器 tick：服务端用 Tick、客户端用插值 ServerTick，
// 实现须处于 LES 实体生命周期内，供 SkillCastValidator 与 UI 共用。
public partial class UnitPawn : IBattleUnit {
    /// <inheritdoc />
    string IBattleUnit.UnitName => UnitName.Value;

    /// <inheritdoc />
    ushort IBattleUnit.UnitNetId => Id;

    /// <inheritdoc />
    IReadOnlyList<string> IBattleUnit.Camps => CampTags;

    /// <inheritdoc />
    UnitSnapshot IBattleUnit.Snapshot => new() {
        Health = Health.Value,
        MaxHealth = MaxHealth.Value,
        PhysicalAttackBase = PhysicalAttackBase.Value,
        PhysicalTakePercent = PhysicalTakePercent.Value,
        MagicAttackBase = MagicAttackBase.Value,
        MagicTakePercent = MagicTakePercent.Value,
        CureIntensity = CureIntensity.Value,
        MoveSpeed = BaseSpeed.Value,
        Position = Position.Value,
        BodyRadius = BodyRadius.Value,
    };

    /// <inheritdoc />
    float IBattleUnit.Health {
        get => Health.Value;
        set => Health.Value = value;
    }

    /// <inheritdoc />
    float IBattleUnit.MaxHealth => MaxHealth.Value;

    /// <inheritdoc />
    SkillKeyId IBattleUnit.SkillCasting {
        get => new(SkillCasting.Value);
        set => SkillCasting.Value = value.Id;
    }

    /// <inheritdoc />
    float IBattleUnit.SkillCastRemaining {
        get => SkillCastRemaining.Value;
        set => SkillCastRemaining.Value = value;
    }

    /// <inheritdoc />
    float IBattleUnit.GcdRemaining {
        get => SyncTickHelper.RemainingSeconds(EntityManager, GcdEndServerTick.Value);
        set => GcdEndServerTick.Value = SyncTickHelper.EndTick(EntityManager, value);
    }

    /// <inheritdoc />
    bool IBattleUnit.HasSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    IReadOnlyList<SkillDefinition> IBattleUnit.Skills => Skills;

    /// <inheritdoc />
    SkillDefinition? IBattleUnit.GetSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return skill;
        }
        return null;
    }

    /// <inheritdoc />
    float IBattleUnit.GetTotalCooldownRemaining(SkillKeyId skillKey) {
        var em = EntityManager;
        float remaining = SyncTickHelper.RemainingSeconds(em, GcdEndServerTick.Value);
        foreach (var cd in SkillCooldowns) {
            if (cd.SkillId == skillKey.Id) {
                float cdRemaining = SyncTickHelper.RemainingSeconds(em, cd.EndServerTick);
                if (cdRemaining > remaining)
                    remaining = cdRemaining;
                break;
            }
        }
        return remaining;
    }

    /// <inheritdoc />
    void IBattleUnit.SetSkillCooldown(SkillKeyId skillKey, float remaining) {
        for (int i = 0; i < SkillCooldowns.Count; i++) {
            if (SkillCooldowns[i].SkillId != skillKey.Id)
                continue;
            if (remaining <= 0f)
                SkillCooldowns.RemoveAt(i);
            else
                SkillCooldowns[i] = new SyncSkillCooldown { SkillId = skillKey.Id, EndServerTick = SyncTickHelper.EndTick(EntityManager, remaining) };
            return;
        }
        if (remaining > 0f)
            SkillCooldowns.Add(new SyncSkillCooldown { SkillId = skillKey.Id, EndServerTick = SyncTickHelper.EndTick(EntityManager, remaining) });
    }

    /// <inheritdoc />
    IReadOnlyList<BuffView> IBattleUnit.Buffs {
        get {
            var views = new List<BuffView>(BuffsList.Count);
            foreach (var b in BuffsList)
                views.Add(MapBuffView(b));
            return views;
        }
    }

    /// <inheritdoc />
    void IBattleUnit.ReplaceBuffs(IReadOnlyList<BuffView> buffs) {
        while (BuffsList.Count > 0)
            BuffsList.RemoveAt(BuffsList.Count - 1);
        foreach (var view in buffs) {
            BuffsList.Add(new SyncBuffData {
                BuffTypeId = view.BuffTypeId,
                EndServerTick = SyncTickHelper.EndTick(EntityManager, view.Remaining),
                StackCount = view.StackCount,
                MaxStackCount = StackFor(view.StackCount),
                DamageType = view.DamageType,
            });
        }
    }

    /// <inheritdoc />
    float IBattleUnit.HateFactor => HateFactor;

    /// <inheritdoc />
    IHateRule IBattleUnit.HateRule => HateRule;

    /// <inheritdoc />
    IReadOnlyList<HateSnapshot> IBattleUnit.Hates {
        get {
            var snapshots = new List<HateSnapshot>(HatesList.Count);
            foreach (var h in HatesList)
                snapshots.Add(new HateSnapshot(h.TargetUnitNetId, h.HateValue));
            return snapshots;
        }
    }

    /// <inheritdoc />
    void IBattleUnit.ReplaceHates(IReadOnlyList<HateSnapshot> hates) {
        while (HatesList.Count > 0)
            HatesList.RemoveAt(HatesList.Count - 1);
        foreach (var snapshot in hates) {
            HatesList.Add(new SyncHateData {
                TargetUnitNetId = snapshot.TargetNetId,
                HateValue = snapshot.Value,
            });
        }
    }

    /// <inheritdoc />
    IUnitIntelligence? IBattleUnit.Intelligence => Intelligence;

    /// <inheritdoc />
    void IBattleUnit.BindAIExecutor(IAiExecutor executor) => _aiExecutor = executor;

    /// <inheritdoc />
    void IBattleUnit.RunAI(IBattleSceneView scene, CampRelationResolver relations) {
        if (Health <= 0f || Intelligence is not { } intelligence || _aiExecutor is not { } executor)
            return;

        var decision = intelligence.Decide(this, scene, relations);
        switch (decision.Kind) {
            case EnemyDecisionKind.Idle:
                executor.SetMovement(this, Vector2.Zero);
                break;

            case EnemyDecisionKind.MoveTo:
                executor.SetMovement(this, decision.MoveDirection);
                break;

            case EnemyDecisionKind.CastSkill:
                SetMovementInput(Vector2.Zero);
                executor.RequestCast(this, decision.SkillId, decision.TargetNetId, decision.TargetPosition);
                break;

            default:
                // 未知决策类型按静止退化，决策器为领域内可控代码，正常不产生
                executor.SetMovement(this, Vector2.Zero);
                break;
        }
    }

    /// <inheritdoc />
    void IBattleUnit.SetMovementInput(Vector2 moveDirection) => SetMovementInput(moveDirection);

    /// <inheritdoc />
    UnitCombatState IBattleUnit.RuntimeState => RuntimeState;

    private BuffView MapBuffView(SyncBuffData b) => new() {
        BuffTypeId = b.BuffTypeId,
        Remaining = SyncTickHelper.RemainingSeconds(EntityManager, b.EndServerTick),
        StackCount = b.StackCount,
        DamageType = b.DamageType,
    };

    private static ushort StackFor(ushort stackCount) => stackCount > 0 ? stackCount : (ushort)1;
}
