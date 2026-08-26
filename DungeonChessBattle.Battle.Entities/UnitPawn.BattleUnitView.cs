using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Combat.Hates;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// UnitPawn 对领域单位只读视图的适配：客户端技能预拦与展示数据源。
/// 只读投影 SyncVar，不暴露写通道；服务端结算权威在 BattleUnit，本适配不参与结算。
/// HateFactor/HateRule 仅服务端仇恨规则使用，客户端占位默认，不产生业务影响。
/// </summary>
public partial class UnitPawn : IBattleUnitView {
    /// <inheritdoc />
    string IUnitIdentityView.UnitName => UnitName.Value;

    /// <inheritdoc />
    ushort IUnitIdentityView.UnitNetId => Id;

    /// <inheritdoc />
    IReadOnlyList<string> IUnitIdentityView.Camps => CampTags;

    /// <inheritdoc />
    UnitSnapshot ICombatStatsView.Snapshot => new() {
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
    float ICombatValuesView.Health => Health.Value;

    /// <inheritdoc />
    SkillKeyId ICombatValuesView.SkillCasting => new(SkillCasting.Value);

    /// <inheritdoc />
    bool ISkillSource.HasSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    IReadOnlyList<SkillDefinition> ISkillSource.Skills => Skills;

    /// <inheritdoc />
    SkillDefinition? ISkillSource.GetSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return skill;
        }
        return null;
    }

    /// <inheritdoc />
    float ISkillSource.GetTotalCooldownRemaining(SkillKeyId skillKey)
        => GetTotalCooldownRemaining(skillKey);

    /// <inheritdoc />
    Vector2 IWorldPoseView.Position => Position.Value;

    /// <inheritdoc />
    float IWorldPoseView.BodyRadius => BodyRadius.Value;

    /// <inheritdoc />
    IReadOnlyList<HateSnapshot> IHateActorView.Hates {
        get {
            var snapshots = new List<HateSnapshot>(HatesList.Count);
            foreach (var h in HatesList)
                snapshots.Add(new HateSnapshot(h.TargetUnitNetId, h.HateValue));
            return snapshots;
        }
    }

    /// <inheritdoc />
    float IHateActorView.HateFactor => 1f;

    /// <inheritdoc />
    IHateRule IHateActorView.HateRule => DefaultHateRule.Instance;
}
