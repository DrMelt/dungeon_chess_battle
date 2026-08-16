using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

// UnitPawn 对 IBattleUnit 接口的适配：把 LES SyncVar/SyncList 映射为领域读写通道。
// 领域结算 BattleEngine 面向 IBattleUnit，不感知网络载体；本文件仅做值映射，无结算逻辑。
public partial class UnitPawn : IBattleUnit {
    /// <inheritdoc />
    string IBattleUnit.UnitName => UnitName.Value;

    /// <inheritdoc />
    ushort IBattleUnit.UnitNetId => Id;

    /// <inheritdoc />
    IReadOnlyList<string> IBattleUnit.Camps => [Camp.Value];

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
        get => GcdRemaining.Value;
        set => GcdRemaining.Value = value;
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
    float IBattleUnit.GetSkillCooldownRemaining(SkillKeyId skillKey) {
        foreach (var cd in SkillCooldowns) {
            if (cd.SkillId == skillKey.Id)
                return cd.Remaining;
        }
        return 0f;
    }

    /// <inheritdoc />
    void IBattleUnit.SetSkillCooldown(SkillKeyId skillKey, float remaining) {
        for (int i = 0; i < SkillCooldowns.Count; i++) {
            if (SkillCooldowns[i].SkillId != skillKey.Id)
                continue;
            if (remaining <= 0f)
                SkillCooldowns.RemoveAt(i);
            else
                SkillCooldowns[i] = new SyncSkillCooldown { SkillId = skillKey.Id, Remaining = remaining };
            return;
        }
        if (remaining > 0f)
            SkillCooldowns.Add(new SyncSkillCooldown { SkillId = skillKey.Id, Remaining = remaining });
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
                Remaining = view.Remaining,
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

    private static BuffView MapBuffView(SyncBuffData b) => new() {
        BuffTypeId = b.BuffTypeId,
        Remaining = b.Remaining,
        StackCount = b.StackCount,
        DamageType = b.DamageType,
    };

    private static ushort StackFor(ushort stackCount) => stackCount > 0 ? stackCount : (ushort)1;
}
