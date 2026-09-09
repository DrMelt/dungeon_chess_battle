namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 内容装配根：内容全部来自 mod，<see cref="ContentBootstrapper"/> 装载数据代码入口时注册。
/// 引擎不再内置任何单位/技能/Buff/副本；无 mod 时注册表为空。
/// 两次装配幂等：第二次以新注册表整体替换旧注册表，键覆盖已由注册顺序保证。
/// </summary>
public static class GameContentHost {
    /// <summary>引擎内容修订号：引擎侧已无内置内容，内容修订由 mod 指纹承担，此值保持稳定。</summary>
    public const string EngineRevision = "0";

    private static readonly Lock Sync = new();
    private static ContentSetRegistry? _registry;
    private static BehaviorCatalog? _catalog;

    /// <summary>当前内容注册表；未装配时自动创建空注册表，内容由 mod 装配填充。</summary>
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
    /// 创建并发布内容注册表：随后由调用方把 mod 经引导上下文注册进来。
    /// 调用方须在把注册表交回前完成全部 mod 注册。
    /// </summary>
    public static ContentSetRegistry CreateRegistry(string fingerprint) {
        lock (Sync)
            return _registry = new ContentSetRegistry(EngineRevision, fingerprint);
    }
}
