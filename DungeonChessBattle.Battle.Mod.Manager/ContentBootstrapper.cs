using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Battle.Mod.Interface;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>内容装配结果：可用的 mod 与装配期错误，装配不因个别 mod 失败而中止。</summary>
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
}

/// <summary>
/// 数据面内容装配引导：逐 mod 装载数据代码入口（ALC） → Initialize 把行为与内容定义注册进引导上下文。
/// 引擎无内置内容，注册表自空开始，mod 按装载顺序同键覆盖。服务器进程与 Godot 客户端共用本装配，两端内容与行为目录同源。
/// 流程由本类钉死，环节各归其位：扫描与入口装载用本库 <see cref="ModLoader"/> 与 <see cref="ModEntryLoader"/>，
/// 注册表与行为目录归 Battle.GameConfig。
/// </summary>
public static class ContentBootstrapper {
    /// <summary>
    /// 用已完成扫描的结果装配：创建空注册表与空行为目录，再逐 mod 装载数据代码入口。
    /// </summary>
    public static ContentBootResult Load(ModLoadResult result) {
        string fingerprint = ContentFingerprint.Compute(result.Mods);

        var catalog = GameContentHost.Behaviors;
        var registry = GameContentHost.CreateRegistry(fingerprint);
        var context = new ModBootstrapContext(catalog, registry);

        var errors = ModEntryLoader.LoadEntries<IModEntry>(
            result.Mods, ModCodeKind.Data, "mod_", "数据代码入口装载失败",
            (entry, _) => entry.Initialize(context));

        UnitRegistry.Rebind(registry);
        DungeonRegistry.Rebind(registry);

        return new ContentBootResult {
            Mods = result.Mods,
            Errors = errors,
            Fingerprint = fingerprint,
        };
    }
}
