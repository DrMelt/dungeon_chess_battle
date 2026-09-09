namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 包的磁盘布局约定中不可配置的那一半：两个文件名与三个默认目录名，以及由目录推导清单路径的算法。
/// 产物本身落在哪里由每个 mod 的 manifest 声明，未声明才回落到这里的默认目录。
/// 装载、指纹、启停与展示面都按 manifest 给出的路径定位，两端共用这里的常量，默认布局只在这里定义。
/// </summary>
public static class ModLayout {
    /// <summary>清单文件名，位于 mod 目录根部。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>启用集文件名，位于 mods 根目录。</summary>
    public const string EnablementFileName = "mods.enabled.json";

    /// <summary>数据代码的默认目录名，manifest 未声明 <c>code</c> 时按它枚举入口 DLL。</summary>
    public const string CodeDirectoryName = "code";

    /// <summary>展示代码的默认目录名，manifest 未声明 <c>codeDisplay</c> 时按它枚举入口 DLL。</summary>
    public const string DisplayCodeDirectoryName = "code_display";

    /// <summary>展示资源的默认目录名，manifest 未声明 <c>packages</c> 时按它枚举待挂载资源包。</summary>
    public const string AssetsDirectoryName = "assets";

    /// <summary>mods 根目录内的清单路径。</summary>
    public static string ManifestOf(string modDirectory) =>
        Path.Combine(modDirectory, ManifestFileName);
}
