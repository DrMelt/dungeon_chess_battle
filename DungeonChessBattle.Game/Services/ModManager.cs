using System.Linq;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Game.GameAssets;
using DungeonChessBattle.Game.GameAssets.Mods;
using DungeonChessBattle.Game.Mod;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// Godot 端 mod 装配编排：扫描启用集 → 数据装配 → 展示注册 → mod 视图落地成资源 → 发布统一展示索引。
/// 主场景 _Ready 首个调用，保证任何 UI 与资源表访问前内容已就绪；
/// 服务器子进程由 ServerProcessHost 注入同一 user://mods，两端读同一启用集与内容即同源。
/// </summary>
public static class ModManager {
    /// <summary>mods 根目录的 Godot 路径挂载点，mod 自带场景经它寻址。</summary>
    public const string ModsRootGodotPath = "user://mods";

    /// <summary>mods 根目录绝对路径。</summary>
    public static string ModsRootPath => ProjectSettings.GlobalizePath(ModsRootGodotPath);

    /// <summary>mod 管理根：列表、启用态与装载错误；未装配为 null。</summary>
    public static ModCatalog? Catalog {
        get; private set;
    }

    private static readonly ILogger Logger = ServiceLocator.CreateLogger(nameof(ModManager));

    private static bool _initialized;

    /// <summary>执行一次装配，幂等；单个 mod 失败不中止其余 mod，错误汇总进 <see cref="Catalog"/>。</summary>
    public static void EnsureInitialized() {
        if (_initialized)
            return;
        _initialized = true;

        var catalog = ModCatalog.Scan(ModsRootPath);
        Catalog = catalog;

        // 复用同一次扫描结果装配数据面：启停文件已随扫描读入，两端不必再传参。
        // Load(扫描结果) 只带回装配期新增错误（数据代码入口装载失败），
        // 扫描期错误已由 ModLoader 记在 catalog.Errors，不重复并入
        var boot = ContentBootstrapper.Load(catalog.ScanResult);
        catalog.RecordAssemblyErrors(boot.Errors);

        // 顺序约束：内容注册表须先就绪（展示键校验依赖它），内置展示先注册、mod 展示后注册，同键条目才被 mod 覆盖
        var display = new DisplayRegistry();
        BuiltinDisplayAssets.Register(display);
        var declared = ModAssets.Initialize(catalog, GameContentHost.Registry, display, ModsRootGodotPath);
        ModAssetsMapper.Apply(GameContentHost.Registry, declared, display);
        ModAssets.Publish(display);

        foreach (string error in catalog.Errors)
            Logger.LogError("mod 数据装载失败: {Error}", error);
        foreach (string error in catalog.AssemblyErrors)
            Logger.LogError("mod 内容装配问题: {Error}", error);
        foreach (string error in catalog.DisplayErrors)
            Logger.LogWarning("mod 展示装配问题: {Error}", error);
        if (Logger.IsEnabled(LogLevel.Information))
            Logger.LogInformation(
                "mod 装配完成：启用 {Enabled} 个，停用 {Disabled} 个，指纹 {Fingerprint}",
                catalog.EnabledMods.Count, catalog.DisabledCount, catalog.Fingerprint);
    }
}
