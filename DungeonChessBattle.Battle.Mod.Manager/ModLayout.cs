namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 包的磁盘布局约定：mods 根目录下每个 mod 一个子目录，目录名即 <c>manifest.Id</c>。
/// 装载、指纹、启停与展示面都按此定位，两端共用同一份常量，布局只在这里定义。
/// </summary>
public static class ModLayout {
    /// <summary>清单文件名，位于 mod 目录根部。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>启用集文件名，位于 mods 根目录。</summary>
    public const string EnablementFileName = "mods.enabled.json";

    /// <summary>数据代码子目录，两端各自 ALC 装载，进内容指纹。</summary>
    public const string CodeDirectoryName = "code";

    /// <summary>展示代码子目录，仅客户端装载，不进内容指纹。</summary>
    public const string DisplayCodeDirectoryName = "code_display";

    /// <summary>mod 目录内的数据代码路径。</summary>
    public static string CodeDirectoryOf(string modDirectory) =>
        Path.Combine(modDirectory, CodeDirectoryName);

    /// <summary>mod 目录内的展示代码路径。</summary>
    public static string DisplayCodeDirectoryOf(string modDirectory) =>
        Path.Combine(modDirectory, DisplayCodeDirectoryName);

    /// <summary>mods 根目录内的清单路径。</summary>
    public static string ManifestOf(string modDirectory) =>
        Path.Combine(modDirectory, ManifestFileName);
}
