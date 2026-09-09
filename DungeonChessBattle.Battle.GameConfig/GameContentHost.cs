namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 内容装配根：当前注册表与行为目录的持有者。引擎不内置任何单位/技能/Buff/副本，内容一律由装配方写入，
/// 数据面装配流程见 Battle.Mod.Manager 的 ContentBootstrapper。
/// 两次装配幂等：第二次以新注册表整体替换旧注册表，键覆盖已由写入顺序保证。
/// </summary>
public static class GameContentHost {
    /// <summary>引擎内容修订号：引擎侧已无内置内容，修订由装配方传入的内容指纹承担，此值保持稳定。</summary>
    public const string EngineRevision = "0";

    private static readonly Lock Sync = new();
    private static ContentSetRegistry? _registry;
    private static BehaviorCatalog? _catalog;

    /// <summary>当前内容注册表；未装配时自动创建空注册表。</summary>
    public static ContentSetRegistry Registry {
        get {
            lock (Sync)
                return _registry ??= CreateRegistry("");
        }
    }

    /// <summary>当前行为目录；内容构造与行为注册共享同一目录实例。</summary>
    public static BehaviorCatalog Behaviors {
        get {
            lock (Sync)
                return _catalog ??= new BehaviorCatalog();
        }
    }

    /// <summary>
    /// 创建并发布内容注册表：内容写入由调用方在这之后完成。
    /// 指纹参与内容修订号，无内容装配时传空串。
    /// </summary>
    public static ContentSetRegistry CreateRegistry(string fingerprint) {
        lock (Sync)
            return _registry = new ContentSetRegistry(EngineRevision, fingerprint);
    }
}
