namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>技能可释放目标类型的标志位。</summary>
[Flags]
public enum SkillTargetPolicy {
    /// <summary>不可主动选择目标释放</summary>
    None = 0,
    /// <summary>可对同阵营单位释放。</summary>
    Same = 1,
    /// <summary>可对敌阵营单位释放。</summary>
    Different = 2,
}
