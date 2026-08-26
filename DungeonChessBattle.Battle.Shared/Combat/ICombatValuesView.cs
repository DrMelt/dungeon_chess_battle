namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 战斗数值只读视图：施法校验与展示共用的标量状态子集。
/// 聚合快照（<see cref="ICombatStatsView.Snapshot"/>）属 <see cref="ICombatStatsView"/>，本接口只暴露判定与展示直接读取的标量。
/// </summary>
public interface ICombatValuesView {
    /// <summary>当前生命值。</summary>
    float Health {
        get;
    }

    /// <summary>当前施法技能，default 表示无施法。</summary>
    SkillKeyId SkillCasting {
        get;
    }
}
