using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GameAssets.Mods;
using DungeonChessBattle.Game.Mod.Manager;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// Godot 端 mod 装配编排：扫描启用集 → 数据面装配 → 展示面装配。
/// 主场景 _Ready 首个调用，保证任何 UI 与资源表访问前内容已就绪；
/// 服务器子进程由 ServerProcessHost 注入同一 user://mods，两端读同一启用集与内容即同源。
/// 展示装配内部的先后次序不在这里，见 <see cref="ModAssets.Assemble"/>。
/// </summary>
public static class ModManager {
    /// <summary>mods 根目录的 Godot 路径挂载点，mod 自带场景经它寻址。</summary>
    // Godot user:// 虚拟路径，非文件系统绝对路径，S1075 误报
#pragma warning disable S1075
    public const string ModsRootGodotPath = "user://mods";
#pragma warning restore S1075

    /// <summary>mods 根目录绝对路径。</summary>
    public static string ModsRootPath => ProjectSettings.GlobalizePath(ModsRootGodotPath);

    private static readonly ILogger Logger = ServiceLocator.CreateLogger(nameof(ModManager));

    private static bool _initialized;

    /// <summary>执行一次装配，幂等；单个 mod 失败不中止其余 mod，错误汇总进 <c>ModAssets.Catalog</c>。</summary>
    public static void EnsureInitialized() {
        if (_initialized)
            return;
        _initialized = true;

        var catalog = ModCatalog.Scan(ModsRootPath);
        // 复用同一次扫描结果装配数据面：启停文件已随扫描读入，两端不必再传参。
        // Load(扫描结果) 只带回装配期新增错误，扫描期错误已在 catalog.Errors
        catalog.RecordAssemblyErrors(ContentBootstrapper.Load(catalog.ScanResult).Errors);

        // 注册表取装配后的实例：内容须先就绪，展示键校验才看得到 mod 注册进来的条目
        ModAssets.Assemble(
            catalog, GameContentHost.Registry, BuiltinDisplayAssets.Register,
            (declared, registry) => ModAssetsMapper.Apply(GameContentHost.Registry, declared, registry),
            ModsRootGodotPath);

        foreach (var error in catalog.Errors)
            Logger.LogError("mod 扫描失败: {Error}", error);
        foreach (var error in catalog.AssemblyErrors)
            Logger.LogError("mod 内容装配问题: {Error}", error);
        foreach (var error in catalog.DisplayErrors)
            Logger.LogWarning("mod 展示装配问题: {Error}", error);
        if (Logger.IsEnabled(LogLevel.Information))
            Logger.LogInformation(
                "mod 装配完成：启用 {Enabled} 个，停用 {Disabled} 个，指纹 {Fingerprint}",
                catalog.EnabledMods.Count, catalog.DisabledCount, catalog.Fingerprint);
    }
}
