using DungeonChessBattle.Battle.Config.Shared;
using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Game.Display.Registry;
using DungeonChessBattle.Game.Mod.Interface;
using DungeonChessBattle.Game.Mod.Shared;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// 一次展示装配的产物：展示注册表与 mod 管理根，全程只读。
/// 本库不持全局状态，实例由宿主工程的组合根持有。
/// 注册表读面恒返回 null 而不抛，保证零 mod 环境直接可用。
/// </summary>
/// <param name="registry">本次装配的展示注册表，条目展示数据所在。</param>
/// <param name="catalog">本次装配所用的 mod 管理根。</param>
/// <param name="assemblyFingerprint">装配那一刻的启用集指纹。</param>
public sealed class ModAssets(DisplayRegistry registry, ModCatalog catalog, string assemblyFingerprint) {
    /// <summary>展示注册表：条目展示数据的唯一读面，按内容键查询。</summary>
    public IDisplayRegistry Registry {
        get;
    } = registry;

    /// <summary>本次装配所用的 mod 管理根。</summary>
    public ModCatalog Catalog {
        get;
    } = catalog;

    /// <summary>装配那一刻的启用集指纹；与 <see cref="ModCatalog.Fingerprint"/> 不等即说明磁盘已改动而未重启。</summary>
    public string AssemblyFingerprint {
        get;
    } = assemblyFingerprint;

    /// <summary>
    /// 展示装配全过程，顺序由本方法保证：建注册表 → 装载展示代码 → 挂载展示资源包 →
    /// 逐 mod 执行入口把声明注册进同表 → 表就绪后对外可查。
    /// </summary>
    /// <remarks>
    /// 资源包挂载一步以委托交入：可被 <c>.tres</c>/<c>.tscn</c> 引用的资源类与 <c>res://</c> 路径
    /// 只能留在 Godot 主工程，本库不认识它们，只负责把顺序钉死在这里。
    /// 装载先于挂载、入口执行后于挂载：入口 Initialize 要按 <c>res://mods/{id}/</c> 前缀自行读包内资源。
    /// 展示注册表在装配结束时即最终态：宿主不再据它二次物化。
    /// </remarks>
    /// <param name="catalog">已扫描的 mod 管理根，提供参与装配的启用 mod、展示声明与错误落点。</param>
    /// <param name="content">内容注册表只读视图，展示键完整性校验对它做。</param>
    /// <param name="mountResourcePacks">逐 mod 挂载它声明的展示资源包，必须介于展示代码装载与入口执行之间。</param>
    /// <param name="loggerFactory">日志通道工厂，未注入时静默。</param>
    /// <returns>装配完成的产物实例，由调用方持有。</returns>
    public static ModAssets Assemble(
        ModCatalog catalog,
        IContentRegistryView content,
        Action<IReadOnlyList<ModDisplayDeclaration>> mountResourcePacks,
        ILoggerFactory? loggerFactory = null) {
        var logger = loggerFactory?.CreateLogger<ModAssets>() ?? NullLogger<ModAssets>.Instance;
        var registry = new DisplayRegistry();

        var runtimeErrors = new List<ModError>();

        // 声明不可用的 mod 已在扫描期排除：数据面已按同一份清单照常装载，展示面缺一块不影响它
        var displays = catalog.EnabledDisplays;
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("展示装配开始：启用 {Count} 个 mod，展示声明不可用 {Blocked} 个",
                catalog.EnabledMods.Count, catalog.EnabledMods.Count - displays.Count);

        // 装载展示代码不执行入口，ALC 由 loaded 持有到入口统一执行完成
        var sources = displays
            .Select(display => new ModEntrySource(display.ModId, display.EntryDlls, display.ProbeDirectories))
            .ToList();
        using var loaded = ModEntryLoader.Load<IModDisplayEntry>(
            sources, "mod_display_", "展示代码入口装载失败", loggerFactory);

        mountResourcePacks(displays);

        var initErrors = ModEntryLoader.Initialize(loaded, "展示代码入口执行失败",
            (entry, modId) => entry.Initialize(
                new ModDisplayRegistrar(registry, content, runtimeErrors, modId),
                new ModDisplayContext(modId, registry)),
            loggerFactory);

        // 展示问题四处产出，收敛到装配结束处落日志一次，条目仍按归属进 catalog
        foreach (var error in loaded.Errors.Concat(runtimeErrors).Concat(initErrors))
            LogDisplayFailed(logger, error);
        catalog.RecordDisplayErrors([.. loaded.Errors, .. runtimeErrors, .. initErrors]);

        return new ModAssets(registry, catalog, catalog.Fingerprint);
    }

    /// <summary>一条展示装配问题：原因已含 mod 归属，条目同时进管理面。</summary>
    private static void LogDisplayFailed(ILogger logger, ModError error) {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError("展示装配失败：{ModId}：{Reason}", error.ModId, error.Message);
    }
}
