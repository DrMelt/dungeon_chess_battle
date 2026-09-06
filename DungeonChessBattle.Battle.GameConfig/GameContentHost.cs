using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 内容装配根：内置基座先注册，mod 内容在 <see cref="ContentBootstrapper"/> 装载数据代码入口时注册。
/// 两次装配幂等：第二次以新注册表整体替换旧注册表，键覆盖已由注册顺序保证。
/// </summary>
public static class GameContentHost {
    private static readonly Lock Sync = new();
    private static ContentSetRegistry? _registry;
    private static BehaviorCatalog? _catalog;

    /// <summary>当前内容注册表；未装配时自动以纯内置内容装配，保证零 mod 环境直接可用。</summary>
    public static ContentSetRegistry Registry {
        get {
            lock (Sync)
                return _registry ??= CreateRegistry("");
        }
    }

    /// <summary>当前行为目录；内容构造与 mod 代码注册共享同一目录实例。</summary>
    public static BehaviorCatalog Behaviors {
        get {
            lock (Sync)
                return _catalog ??= new BehaviorCatalog();
        }
    }

    /// <summary>
    /// 创建并发布内容注册表：内置基座先注册全部内置内容，随后由调用方把 mod 经引导上下文注册进来。
    /// 调用方须在把注册表交回前完成全部 mod 注册；失败时用内置内容重建并回退。
    /// </summary>
    public static ContentSetRegistry CreateRegistry(string fingerprint) {
        lock (Sync) {
            var registry = new ContentSetRegistry(BuiltInContent.BuiltInRevision, fingerprint);
            BuiltInContent.Register(registry, Behaviors);
            return _registry = registry;
        }
    }
}
