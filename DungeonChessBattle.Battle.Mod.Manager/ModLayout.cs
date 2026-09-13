namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 包的磁盘布局约定中不可配置的那一半：清单与启用集两个文件名、清单内展示段的键名，
/// 以及由目录推导清单路径的算法。
/// 产物位置一律由每个 mod 的 manifest 显式声明，本类不提供默认目录。
/// 装载、指纹与启停按数据面声明的路径定位；展示面声明取自同一份清单，由 Game.Mod.Manager 自行读取定位。
/// </summary>
public static class ModLayout {
    /// <summary>清单文件名，位于 mod 目录根部。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>启用集文件名，位于 mods 根目录。</summary>
    public const string EnablementFileName = "mods.enabled.json";

    /// <summary>
    /// 清单内展示面声明段的键名。段内容归 Game.Mod.Manager 解释，数据面只按此名登记该段存在与否。
    /// 两侧引用同一常量，省得段名在各自代码里各写一遍。
    /// </summary>
    public const string ManifestDisplaySection = "display";

    /// <summary>mods 根目录内的清单路径。</summary>
    public static string ManifestOf(string modDirectory) =>
        Path.Combine(modDirectory, ManifestFileName);
}
