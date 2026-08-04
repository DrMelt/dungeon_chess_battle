namespace DungeonChessBattle.Core.Enums;

/// <summary>
/// 阵营字符串常量，替代原 EnumCamp 枚举。
/// 一个单位可属于多个阵营，通过 List<string> 表示。
/// </summary>
public static class CampConstants {
    public const string CampA = "Camp_A";
    public const string CampB = "Camp_B";
    public const string CampBoss = "Camp_BOSS";
}
