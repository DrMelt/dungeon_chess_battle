namespace DungeonChessBattle.Battle.Domain.Enums;

/// <summary>
/// 目标相对本地玩家的阵营关系，用于技能判定与 UI 着色。
/// </summary>
public enum CampRelation : byte {
    /// <summary>未判定：关系尚未观测到，或关系函数对未覆盖组合显式兜底。区别于中立。</summary>
    Unknown = 0,
    /// <summary>友方。</summary>
    Friendly = 1,
    /// <summary>中立。</summary>
    Neutral = 2,
    /// <summary>敌方。</summary>
    Enemy = 3,
}
