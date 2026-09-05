using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>内容装配结果：可用的 mod 与逐项错误，装配不因个别 mod 失败而中止。</summary>
public sealed class ContentBootResult {
    /// <summary>成功装载并参与内容的 mod。</summary>
    public required IReadOnlyList<LoadedMod> Mods {
        get; init;
    }

    /// <summary>装载错误说明；空表示全部成功。</summary>
    public required IReadOnlyList<string> Errors {
        get; init;
    }

    /// <summary>本次装配的内容指纹；无 mod 时为空串。</summary>
    public required string Fingerprint {
        get; init;
    }
}

/// <summary>
/// 内容引导装配：mods 根目录 → 装载代码 mod（ALC） → 合并数据 → 重建全局注册表与目录单例。
/// 服务器进程与 Godot 客户端共用本装配，保证两端内容与行为目录一致。
/// </summary>
public static class ContentBootstrapper {
    /// <summary>
    /// 装配 mods 根目录下的全部 mod；目录不存在或为空时按纯内置内容装配。
    /// mod 代码程序集装载失败不中断其余 mod，错误经返回值汇总。
    /// </summary>
    public static ContentBootResult Load(string? modRoot) {
        var result = ModLoader.LoadDirectory(modRoot ?? "");

        // 代码 mod 必须注册完行为再合并数据编译，行为 ID 才能被 content.json 引用
        var catalog = GameContentHost.Behaviors;
        foreach (var mod in result.Mods)
            LoadCodeAssemblies(mod, catalog);

        GameContentHost.Configure(result.Mods);
        UnitRegistry.Rebind(GameContentHost.Registry);
        DungeonRegistry.Rebind(GameContentHost.Registry);

        return new ContentBootResult {
            Mods = result.Mods,
            Errors = result.Errors,
            Fingerprint = ContentFingerprint.Compute(result.Mods),
        };
    }

    private static void LoadCodeAssemblies(LoadedMod mod, BehaviorCatalog catalog) {
        string codeDir = Path.Combine(mod.DirectoryPath, ModLoader.CodeDirectoryName);
        if (!Directory.Exists(codeDir))
            return;

        foreach (string dll in Directory.GetFiles(codeDir, "*.dll", SearchOption.TopDirectoryOnly)) {
            using var loader = new ModAssemblyLoader($"mod_{mod.Manifest.Id}");
            loader.AddDependencyDirectory(codeDir);
            var entry = loader.LoadEntry(dll);
            entry?.Initialize(catalog);
        }
        // Initialize 注册的 factory 委托强引用本 ALC 内类型，Dispose 的 Unload 不会真正回收程序集；
        // 行为实例照常可调用，当前单次装配模型下可接受，代价是不支持 mod 热重载
    }
}
