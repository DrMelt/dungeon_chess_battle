using DungeonChessBattle.Battle.Config.Registry;
using DungeonChessBattle.Battle.Mod.Interface;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>内容装配结果：装配产出的内容、参与内容的 mod 与装配期错误；装配不因个别 mod 失败而中止。</summary>
public sealed class ContentBootResult {
    /// <summary>成功装载并参与内容的 mod。</summary>
    public required IReadOnlyList<LoadedMod> Mods {
        get; init;
    }

    /// <summary>装配期新增错误：数据代码入口装载失败。扫描期错误由装载结果携带，不在此重复。</summary>
    public required IReadOnlyList<ModError> Errors {
        get; init;
    }

    /// <summary>本次装配的内容指纹；无 mod 时为空串。</summary>
    public required string Fingerprint {
        get; init;
    }

    /// <summary>本次装配产出的内容注册表，由调用方持有。</summary>
    public required ContentSetRegistry Registry {
        get; init;
    }
}

/// <summary>
/// 数据面内容装配引导：逐 mod 装载数据代码入口（ALC） → Initialize 把内容定义注册进引导上下文。
/// 引擎无内置内容，注册表每次装配新建，mod 按装载顺序同键覆盖。服务器进程与 Godot 客户端共用本装配，两端内容同源。
/// 产物以 <see cref="ContentSetRegistry"/> 交回调用方持有，本类不持全局状态。
/// 流程由本类钉死，环节各归其位：扫描与入口装载用本库 <see cref="ModLoader"/> 与 <see cref="ModEntryLoader"/>，
/// 注册表归 Battle.Config.Registry。
/// </summary>
public static class ContentBootstrapper {
    /// <summary>引擎内容修订号：引擎侧已无内置内容，修订由装配方传入的内容指纹承担，此值保持稳定。</summary>
    public const string EngineRevision = "0";

    /// <summary>
    /// 用已完成扫描的结果装配：新建空注册表，再逐 mod 装载数据代码入口。
    /// 装配的起止与数据面条目计数记 Information，两端同形便于按指纹比对。
    /// </summary>
    public static ContentBootResult Load(ModLoadResult result, ILoggerFactory? loggerFactory = null) {
        var logger = loggerFactory?.CreateLogger(typeof(ContentBootstrapper).FullName!) ?? NullLogger.Instance;
        string fingerprint = ContentFingerprint.Compute(result.Mods);

        var registry = new ContentSetRegistry(EngineRevision, fingerprint);
        var context = new ModBootstrapContext(registry, loggerFactory ?? NullLoggerFactory.Instance);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("内容装配开始：启用 {Count} 个 mod，指纹 {Fingerprint}",
                result.Mods.Count, string.IsNullOrEmpty(fingerprint) ? "无" : fingerprint);

        var sources = result.Mods
            .Select(mod => new ModEntrySource(mod.Manifest.Id, mod.CodeEntries, mod.CodeLibraries))
            .ToList();
        var errors = ModEntryLoader.LoadEntries<IModEntry>(
            sources, "mod_", "数据代码入口装载失败",
            (entry, _) => entry.Initialize(context), loggerFactory);

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("内容装配完成：技能 {Skills}/Buff {Buffs}/单位 {Units}/可选单位 {Selectable}/副本 {Dungeons}",
                registry.Skills.Count, registry.Buffs.Count, registry.Units.Count,
                registry.GetPlayerSelectableUnits().Count, registry.Dungeons.Count);

        return new ContentBootResult {
            Mods = result.Mods,
            Errors = errors,
            Fingerprint = fingerprint,
            Registry = registry,
        };
    }
}
