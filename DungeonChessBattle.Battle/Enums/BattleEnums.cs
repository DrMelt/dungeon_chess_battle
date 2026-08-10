namespace DungeonChessBattle.Battle.Enums;

/// <summary>伤害类型。</summary>
public enum DamageType {
    /// <summary>无伤害。</summary>
    None = 0,
    /// <summary>物理伤害。</summary>
    Physical,
    /// <summary>魔法伤害。</summary>
    Magic,
}

/// <summary>技能可释放目标类型的 Flag 位标志。通过 HasFlag 判断目标属于同阵营或敌阵营。</summary>
[Flags]
public enum SkillCanAdd {
    /// <summary>无类型限制。</summary>
    None = 0,
    /// <summary>可对同阵营单位释放。</summary>
    Same = 1,
    /// <summary>可对敌阵营单位释放。</summary>
    Different = 2,
}

/// <summary>战斗阶段（实时化：不支持回合制）。</summary>
public enum BattlePhase : byte {
    /// <summary>等待开始（大厅→战斗过渡）。</summary>
    Waiting,
    /// <summary>战斗中（实时 Tick）。</summary>
    Running,
    /// <summary>战斗结束。</summary>
    Finished,
}
