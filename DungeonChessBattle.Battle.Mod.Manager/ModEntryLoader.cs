using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// 一个 mod 的一类入口装载输入：身份、入口 DLL 绝对路径与依赖探测目录。
/// 数据入口与展示入口都用它表达，装载器不区分两者的语义。
/// </summary>
/// <param name="ModId">mod ID，用于 ALC 命名与错误归属。</param>
/// <param name="EntryDlls">入口 DLL 绝对路径，顺序即装载顺序。</param>
/// <param name="ProbeDirectories">依赖探测目录绝对路径，含各入口自身所在目录。</param>
public sealed record ModEntrySource(
    string ModId, IReadOnlyList<string> EntryDlls, IReadOnlyList<string> ProbeDirectories);

/// <summary>
/// 已装载待执行的 mod 入口集合：持有入口实例与其 ALC 直到入口统一执行完成。
/// <c>ModEntryLoader.Load</c> 产出、<c>ModEntryLoader.Initialize</c> 消费后由调用方释放。
/// </summary>
public sealed class LoadedModEntries<TEntry> : IDisposable where TEntry : class {
    internal readonly List<(TEntry Entry, string ModId, string DllPath)> Items = [];
    internal readonly List<ModAssemblyLoader> Loaders = [];
    private readonly List<ModError> _errors = [];

    /// <summary>装载期错误；入口执行期错误见 <c>ModEntryLoader.Initialize</c> 返回值。</summary>
    public IReadOnlyList<ModError> Errors => _errors;

    internal void AddError(ModError error) => _errors.Add(error);

    /// <summary>释放持有的全部 ALC。入口注册的委托强引用其内类型，Unload 不真正回收程序集，实例仍可调用。</summary>
    public void Dispose() {
        foreach (var loader in Loaders)
            loader.Dispose();
        Loaders.Clear();
    }
}

/// <summary>
/// mod 代码入口装载器：遍历入口来源，逐个装载它声明的入口 DLL，以独立 ALC 找到
/// 指定的入口接口实现并实例化。数据入口与展示入口共用这一段边界，
/// 装载器不感知入口接口本身，也不知道注册了什么，更不区分调用方是哪一面。
/// 入口清单为空即该来源不贡献这类代码，静默跳过；单个 DLL 失败记一条 <see cref="ModError"/> 并继续其余。
/// 装载与入口执行拆为两阶段：数据面无资源顺序需求，<c>LoadEntries</c> 一步完成；
/// 展示面先 <c>Load</c>、居中挂载展示资源包、再 <c>Initialize</c> 执行入口。
/// </summary>
public static class ModEntryLoader {
    /// <summary>
    /// 装载并立即执行入口：逐来源装载入口程序集，实例化后交 <paramref name="initialize"/> 初始化。
    /// </summary>
    /// <param name="sources">入口来源，顺序即装载顺序。</param>
    /// <param name="alcNamePrefix">ALC 名前缀，便于诊断区分装载方。</param>
    /// <param name="failureText">失败条目的原因前缀，如「数据代码入口装载失败」。</param>
    /// <param name="initialize">入口实例与所属 mod ID 的配对回调，注册动作在其中发生。</param>
    /// <param name="loggerFactory">日志通道工厂，未注入时静默。</param>
    public static List<ModError> LoadEntries<TEntry>(
        IReadOnlyList<ModEntrySource> sources,
        string alcNamePrefix,
        string failureText,
        Action<TEntry, string> initialize,
        ILoggerFactory? loggerFactory = null) where TEntry : class {
        using var loaded = Load<TEntry>(sources, alcNamePrefix, failureText, loggerFactory);
        return [.. loaded.Errors, .. Initialize(loaded, failureText, initialize, loggerFactory)];
    }

    /// <summary>
    /// 阶段一：仅装载不执行。逐来源装载入口程序集并实例化，成功装载的 ALC 由返回集合持有，
    /// 供挂载等中间步骤完成后统一执行入口。
    /// 入口清单的顺序即装载顺序，装载侧不再枚举目录——枚举顺序由文件系统决定，两端可不一致。
    /// </summary>
    public static LoadedModEntries<TEntry> Load<TEntry>(
        IReadOnlyList<ModEntrySource> sources,
        string alcNamePrefix,
        string failureText,
        ILoggerFactory? loggerFactory = null) where TEntry : class {
        var logger = loggerFactory?.CreateLogger(typeof(ModEntryLoader).FullName!) ?? NullLogger.Instance;
        var loaded = new LoadedModEntries<TEntry>();
        foreach (var source in sources) {
            foreach (string dll in source.EntryDlls) {
                string alcName = $"{alcNamePrefix}{source.ModId}";
                var loader = new ModAssemblyLoader(alcName, loggerFactory?.CreateLogger<ModAssemblyLoader>());
                try {
                    foreach (string directory in source.ProbeDirectories)
                        loader.AddDependencyDirectory(directory);

                    LogEntryLoading(logger, source.ModId, dll, alcName);
                    var entry = loader.LoadEntry<TEntry>(dll);
                    if (entry is not null) {
                        loaded.Items.Add((entry, source.ModId, dll));
                        loaded.Loaders.Add(loader);
                    }
                    else {
                        loader.Dispose();
                        var error = new ModError(source.ModId,
                            $"{failureText} {Path.GetFileName(dll)}: DLL 未包含入口接口 {typeof(TEntry).Name} 的实现");
                        loaded.AddError(error);
                        LogEntryLoadFailed(logger, error);
                    }
                }
                catch (Exception ex) {
                    loader.Dispose();
                    var error = new ModError(
                        source.ModId, $"{failureText} {Path.GetFileName(dll)}: {ex.Message}");
                    loaded.AddError(error);
                    LogEntryLoadFailed(logger, error, ex);
                }
            }
        }
        return loaded;
    }

    /// <summary>
    /// 阶段二：执行全部已装载入口。装载期在 <paramref name="loaded"/> 内的 ALC 此刻仍存活；
    /// 单个入口抛异常记一条 <see cref="ModError"/> 并继续其余。
    /// </summary>
    public static List<ModError> Initialize<TEntry>(
        LoadedModEntries<TEntry> loaded,
        string failureText,
        Action<TEntry, string> initialize,
        ILoggerFactory? loggerFactory = null) where TEntry : class {
        var logger = loggerFactory?.CreateLogger(typeof(ModEntryLoader).FullName!) ?? NullLogger.Instance;
        var errors = new List<ModError>();
        foreach (var (entry, modId, dll) in loaded.Items) {
            if (logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("执行入口 {Dll}：mod {ModId}", Path.GetFileName(dll), modId);
            try {
                initialize(entry, modId);
            }
            catch (Exception ex) {
                var error = new ModError(
                    modId, $"{failureText} {Path.GetFileName(dll)}: {ex.Message}");
                errors.Add(error);
                if (logger.IsEnabled(LogLevel.Error))
                    logger.LogError(ex, "入口执行失败：{Reason}", error.Message);
            }
        }
        return errors;
    }

    #region 日志

    private static void LogEntryLoading(ILogger logger, string modId, string dll, string alcName) {
        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("装载入口程序集 {Dll}：mod {ModId}，ALC {AlcName}",
                Path.GetFileName(dll), modId, alcName);
    }

    private static void LogEntryLoadFailed(ILogger logger, ModError error, Exception? exception = null) {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError(exception, "入口装载失败：{Reason}", error.Message);
    }

    #endregion
}
