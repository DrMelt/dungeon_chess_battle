using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>内容装配结果：可用的 mod 与逐项错误，装配不因个别 mod 失败而中止。</summary>
public sealed class ContentBootResult {
    /// <summary>成功装载并参与内容的 mod。</summary>
    public required IReadOnlyList<LoadedMod> Mods {
        get; init;
    }

    /// <summary>错误说明；空表示无错误。随入口分两种含义：Load(根目录) 为扫描与装配全集，Load(扫描结果) 只含装配期新增。</summary>
    public required IReadOnlyList<string> Errors {
        get; init;
    }

    /// <summary>本次装配的内容指纹；无 mod 时为空串。</summary>
    public required string Fingerprint {
        get; init;
    }
}

/// <summary>
/// 内容引导装配：mods 根目录 → 内置基座入注册表 → 逐 mod 装载数据代码入口（ALC） →
/// Initialize 把行为与内容定义注册进引导上下文。服务器进程与 Godot 客户端共用本装配，保证两端内容与行为目录一致。
/// </summary>
public static class ContentBootstrapper {
    /// <summary>
    /// 装配 mods 根目录下的全部 mod；目录不存在或为空时按纯内置内容装配。
    /// </summary>
    public static ContentBootResult Load(string? modRoot) {
        var result = ModLoader.LoadDirectory(modRoot ?? "");
        var boot = Load(result);
        return new ContentBootResult {
            Mods = boot.Mods,
            Errors = [.. result.Errors, .. boot.Errors],
            Fingerprint = boot.Fingerprint,
        };
    }

    /// <summary>
    /// 用已完成扫描的结果装配：调用方已自行扫描过 mods 目录时走此入口，避免二次扫描。
    /// 扫描期错误由扫描方持有，返回值只带装配期新增错误，不重复并入。
    /// </summary>
    public static ContentBootResult Load(ModLoadResult result) {
        var errors = new List<string>();
        string fingerprint = ContentFingerprint.Compute(result.Mods);

        var catalog = GameContentHost.Behaviors;
        var registry = GameContentHost.CreateRegistry(fingerprint);
        var context = new ModBootstrapContext(catalog, registry);

        foreach (var mod in result.Mods)
            LoadDataAssemblies(mod, context, errors);

        UnitRegistry.Rebind(registry);
        DungeonRegistry.Rebind(registry);

        return new ContentBootResult {
            Mods = result.Mods,
            Errors = errors,
            Fingerprint = fingerprint,
        };
    }

    private static void LoadDataAssemblies(LoadedMod mod, IModBootstrapContext context, List<string> errors) {
        string codeDir = Path.Combine(mod.DirectoryPath, ModLoader.CodeDirectoryName);
        if (!Directory.Exists(codeDir))
            return; // 纯展示 mod 无数据代码，内容贡献为空

        foreach (string dll in Directory.GetFiles(codeDir, "*.dll", SearchOption.TopDirectoryOnly)) {
            try {
                using var loader = new ModAssemblyLoader($"mod_{mod.Manifest.Id}");
                loader.AddDependencyDirectory(codeDir);
                loader.LoadEntry<IModEntry>(dll)?.Initialize(context);
            }
            catch (Exception ex) {
                errors.Add($"{mod.Manifest.Id}: 数据代码入口装载失败 {Path.GetFileName(dll)}: {ex.Message}");
            }
        }
        // Initialize 注册的 factory 委托强引用本 ALC 内类型，Dispose 的 Unload 不会真正回收程序集；
        // 行为实例照常可调用，当前单次装配模型下可接受，代价是不支持 mod 热重载
    }
}
