namespace DungeonChessBattle.Battle.Shared.Enums;

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

    /// <summary>单单位最大阵营数。</summary>
    public const int MaxCampsPerUnit = 3;

    /// <summary>
    /// 判断一组阵营标识是否合法：非空、不超上限、无重复且每项合法。
    /// </summary>
    /// <param name="camps">阵营标识列表。</param>
    /// <returns>合法返回 true。</returns>
    public static bool IsValidCamps(IReadOnlyList<string>? camps) {
        if (camps == null || camps.Count == 0 || camps.Count > MaxCampsPerUnit)
            return false;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        return camps.All(camp => IsValidCamp(camp) && seen.Add(camp));
    }

    /// <summary>判断两个阵营列表是否存在共同阵营；null 或空视为无交集。</summary>
    public static bool HasAnyCamp(IReadOnlyList<string>? a, IReadOnlyList<string>? b) {
        if (a == null || b == null || a.Count == 0 || b.Count == 0)
            return false;
        return a.Any(camp => b.Contains(camp));
    }
}
