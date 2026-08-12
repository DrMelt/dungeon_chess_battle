namespace DungeonChessBattle.Battle.Domain.Enums;

/// <summary>
/// 阵营字符串常量，替代原 EnumCamp 枚举。
/// 一个单位可属于多个阵营，通过字符串列表表示。
/// 网络协议层，JSON、LES SyncVar 与 RPC，直接传输该字符串值，不再使用 byte 编码。
/// </summary>
public static class CampConstants {
    /// <summary>A 方阵营标识，字符串值 "Camp_A"。</summary>
    public const string CampA = "Camp_A";

    /// <summary>B 方阵营标识，字符串值 "Camp_B"。</summary>
    public const string CampB = "Camp_B";

    /// <summary>Boss 阵营标识，字符串值 "Camp_BOSS"。</summary>
    public const string CampBoss = "Camp_BOSS";

    /// <summary>
    /// 判断字符串是否为合法阵营标识。
    /// </summary>
    /// <param name="camp">要校验的阵营字符串。</param>
    /// <returns>合法返回 true。</returns>
    public static bool IsValidCamp(string? camp) =>
        camp is CampA or CampB or CampBoss;
}
