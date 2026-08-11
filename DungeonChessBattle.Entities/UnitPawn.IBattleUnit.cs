using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Entities;

// UnitPawn 对 IBattleUnit 接口的适配：把 LES SyncVar/SyncList 映射为领域读写通道。
// 领域结算 BattleRoom 面向 IBattleUnit，不感知网络载体；本文件仅做值映射，无结算逻辑。
public partial class UnitPawn : IBattleUnit {
    /// <inheritdoc />
    string IBattleUnit.UnitName => UnitName.Value;

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
    ushort IBattleUnit.SkillCasting {
        get => SkillCasting.Value;
        set => SkillCasting.Value = value;
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
    IReadOnlyDictionary<ushort, float> IBattleUnit.SkillCooldowns {
        get {
            var map = new Dictionary<ushort, float>(SkillCooldowns.Count);
            foreach (var cd in SkillCooldowns)
                map[cd.SkillId] = cd.Remaining;
            return map;
        }
    }

    /// <inheritdoc />
    void IBattleUnit.SetSkillCooldown(ushort skillId, float remaining) {
        for (int i = 0; i < SkillCooldowns.Count; i++) {
            if (SkillCooldowns[i].SkillId != skillId)
                continue;
            if (remaining <= 0f)
                SkillCooldowns.RemoveAt(i);
            else
                SkillCooldowns[i] = new SyncSkillCooldown { SkillId = skillId, Remaining = remaining };
            return;
        }
        if (remaining > 0f)
            SkillCooldowns.Add(new SyncSkillCooldown { SkillId = skillId, Remaining = remaining });
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

    private static BuffView MapBuffView(SyncBuffData b) => new() {
        BuffTypeId = b.BuffTypeId,
        Remaining = b.Remaining,
        StackCount = b.StackCount,
        DamageType = b.DamageType,
    };

    private static ushort StackFor(ushort stackCount) => stackCount > 0 ? stackCount : (ushort)1;
}
