using DungeonChessBattle.Battle.Mod;
using DungeonChessBattle.Battle.Mod.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 内容装配根：内置基座与启用 mod 合并后构建 <see cref="ContentSetRegistry"/> 的唯一入口。
/// 两次装配幂等：第二次以新内容集整体替换旧注册表，重复键已由 ContentMerge 后写覆盖，无需显式清理。
/// </summary>
public static class GameContentHost {
    private static readonly Lock Sync = new();
    private static ContentSetRegistry? _registry;
    private static BehaviorCatalog? _catalog;

    /// <summary>当前内容注册表；未装配时自动以纯内置内容装配，保证零 mod 环境直接可用。</summary>
    public static ContentSetRegistry Registry {
        get {
            lock (Sync)
                return _registry ??= Build(BuiltInContent.Create(), "");
        }
    }

    /// <summary>当前行为目录；内容编译与 mod 代码注册共享同一目录实例。</summary>
    public static BehaviorCatalog Behaviors {
        get {
            lock (Sync)
                return _catalog ??= new BehaviorCatalog();
        }
    }

    /// <summary>
    /// 装配内容：以内置基座为底，把按优先级排序的 mod 内容依次合并，再整体编译。
    /// mod 代码入口已在本次装配前经 <see cref="Behaviors"/> 注册。
    /// </summary>
    public static void Configure(IReadOnlyList<LoadedMod> mods) {
        var contents = new List<ModContentJson> { BuiltInContent.Create() };
        contents.AddRange(mods.Select(m => m.Content));
        var merged = ContentMerge.Merge(contents);
        string fingerprint = ContentFingerprint.Compute(mods);

        lock (Sync) {
            _registry = Build(merged, fingerprint);
        }
    }

    /// <summary>以纯内置内容装配，等价于无 mod 启动。</summary>
    public static void ConfigureBuiltInOnly() => Configure([]);

    private static ContentSetRegistry Build(ModContentJson merged, string fingerprint) {
        var catalog = Behaviors;
        return new ContentSetRegistry(merged, BuiltInContent.BuiltInRevision, fingerprint, catalog);
    }
}
