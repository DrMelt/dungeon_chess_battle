using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Combat.Hates;

namespace DungeonChessBattle.Entities;

/// <summary>
/// UnitPawn 对 <see cref="IBattleUnitView"/> 只读视图的适配：客户端技能预拦与展示数据源。
/// 只读投影 SyncVar，不暴露写通道；服务端结算权威在 BattleUnit，本适配不参与结算。
/// HateFactor/HateRule 仅服务端仇恨规则使用，客户端占位默认，不产生业务影响。
/// </summary>
public partial class UnitPawn : IBattleUnitView {
    /// <inheritdoc />
    string IBattleUnitView.UnitName => UnitName.Value;

    /// <inheritdoc />
    ushort IBattleUnitView.UnitNetId => Id;

    /// <inheritdoc />
    IReadOnlyList<string> IBattleUnitView.Camps => CampTags;

    /// <inheritdoc />
    UnitSnapshot IBattleUnitView.Snapshot => new() {
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
    float IBattleUnitView.Health => Health.Value;

    /// <inheritdoc />
    SkillKeyId IBattleUnitView.SkillCasting => new(SkillCasting.Value);

    /// <inheritdoc />
    bool IBattleUnitView.HasSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return true;
        }
        return false;
    }

    /// <inheritdoc />
    IReadOnlyList<SkillDefinition> IBattleUnitView.Skills => Skills;

    /// <inheritdoc />
    SkillDefinition? IBattleUnitView.GetSkill(SkillKeyId skillKey) {
        foreach (var skill in Skills) {
            if (skill.SkillId == skillKey)
                return skill;
        }
        return null;
    }

    /// <inheritdoc />
    float IBattleUnitView.GetTotalCooldownRemaining(SkillKeyId skillKey)
        => GetTotalCooldownRemaining(skillKey);

    /// <inheritdoc />
    IReadOnlyList<HateSnapshot> IBattleUnitView.Hates {
        get {
            var snapshots = new List<HateSnapshot>(HatesList.Count);
            foreach (var h in HatesList)
                snapshots.Add(new HateSnapshot(h.TargetUnitNetId, h.HateValue));
            return snapshots;
        }
    }

    /// <inheritdoc />
    float IBattleUnitView.HateFactor => 1f;

    /// <inheritdoc />
    IHateRule IBattleUnitView.HateRule => DefaultHateRule.Instance;
}
