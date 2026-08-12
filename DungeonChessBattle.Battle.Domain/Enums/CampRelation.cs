namespace DungeonChessBattle.Battle.Domain.Enums;

/// <summary>
/// 目标相对本地玩家的阵营关系，用于 UI 着色。
/// </summary>
public enum CampRelation : byte {
    /// <summary>友方。</summary>
    Friendly = 0,
    /// <summary>中立。</summary>
    Neutral = 1,
    /// <summary>敌方。</summary>
    Enemy = 2,
}
