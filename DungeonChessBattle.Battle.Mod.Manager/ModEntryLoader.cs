namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// 已装载待执行的 mod 入口集合：持有入口实例与其 ALC 直到入口统一执行完成。
/// <c>ModEntryLoader.Load</c> 产出、<c>ModEntryLoader.Initialize</c> 消费后由调用方释放。
/// </summary>
public sealed class LoadedModEntries<TEntry> : IDisposable where TEntry : class {
    internal readonly List<(TEntry Entry, LoadedMod Mod, string DllPath)> Items = [];
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

/// <summary>装载哪一类代码：数据面两端都装并进内容指纹，展示面仅客户端装且不进指纹。</summary>
public enum ModCodeKind {
    /// <summary>数据入口：服务端与客户端都装载，进内容指纹。</summary>
    Data,

    /// <summary>展示入口：仅客户端装载，不进内容指纹。</summary>
    Display,
}

/// <summary>
/// mod 代码入口装载器：遍历 mod，逐个装载它声明的入口 DLL，以独立 ALC 找到
/// 指定的入口接口实现并实例化。两端与展示面共用这一段边界，
/// 装载器不感知入口契约本身，也不知道注册了什么。
/// 入口清单为空即该 mod 不贡献这类代码，静默跳过；单个 DLL 失败记一条 <see cref="ModError"/> 并继续其余。
/// 装载与入口执行拆为两阶段：数据面无资源顺序需求，<c>LoadEntries</c> 一步完成；
/// 展示面先 <c>Load</c>、居中挂载展示资源包、再 <c>Initialize</c> 执行入口。
/// </summary>
public static class ModEntryLoader {
    /// <summary>
    /// 装载并立即执行入口：逐 mod 装载 <paramref name="kind"/> 声明的入口程序集，
    /// 实例化后交 <paramref name="initialize"/> 初始化。
    /// </summary>
    /// <param name="mods">参与装载的 mod，顺序即装载顺序。</param>
    /// <param name="kind">装载数据面还是展示面的入口，产物路径由 <see cref="LoadedMod"/> 携带。</param>
    /// <param name="alcNamePrefix">ALC 名前缀，便于诊断区分数据面与展示面上下文。</param>
    /// <param name="failureText">失败条目的原因前缀，如「数据代码入口装载失败」。</param>
    /// <param name="initialize">入口实例与该 mod 的配对回调，注册动作在其中发生。</param>
    public static List<ModError> LoadEntries<TEntry>(
        IReadOnlyList<LoadedMod> mods,
        ModCodeKind kind,
        string alcNamePrefix,
        string failureText,
        Action<TEntry, LoadedMod> initialize) where TEntry : class {
        using var loaded = Load<TEntry>(mods, kind, alcNamePrefix, failureText);
        return [.. loaded.Errors, .. Initialize(loaded, failureText, initialize)];
    }

    /// <summary>
    /// 阶段一：仅装载不执行。逐 mod 装载 <paramref name="kind"/> 声明的入口程序集并实例化，
    /// 成功装载的 ALC 由返回集合持有，供挂载等中间步骤完成后统一执行入口。
    /// 入口清单的顺序即装载顺序，装载侧不再枚举目录——枚举顺序由文件系统决定，两端可不一致。
    /// </summary>
    public static LoadedModEntries<TEntry> Load<TEntry>(
        IReadOnlyList<LoadedMod> mods,
        ModCodeKind kind,
        string alcNamePrefix,
        string failureText) where TEntry : class {
        var loaded = new LoadedModEntries<TEntry>();
        foreach (var mod in mods) {
            var entries = kind == ModCodeKind.Data ? mod.CodeEntries : mod.DisplayEntries;
            var libraries = kind == ModCodeKind.Data ? mod.CodeLibraries : mod.DisplayLibraries;
            foreach (string dll in entries) {
                var loader = new ModAssemblyLoader($"{alcNamePrefix}{mod.Manifest.Id}");
                try {
                    foreach (string directory in libraries)
                        loader.AddDependencyDirectory(directory);

                    var entry = loader.LoadEntry<TEntry>(dll);
                    if (entry is not null) {
                        loaded.Items.Add((entry, mod, dll));
                        loaded.Loaders.Add(loader);
                    }
                    else {
                        loader.Dispose();
                        loaded.AddError(new ModError(mod.Manifest.Id,
                            $"{failureText} {Path.GetFileName(dll)}: DLL 未包含入口接口 {typeof(TEntry).Name} 的实现"));
                    }
                }
                catch (Exception ex) {
                    loader.Dispose();
                    loaded.AddError(new ModError(
                        mod.Manifest.Id, $"{failureText} {Path.GetFileName(dll)}: {ex.Message}"));
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
        Action<TEntry, LoadedMod> initialize) where TEntry : class {
        var errors = new List<ModError>();
        foreach (var (entry, mod, dll) in loaded.Items) {
            try {
                initialize(entry, mod);
            }
            catch (Exception ex) {
                errors.Add(new ModError(
                    mod.Manifest.Id, $"{failureText} {Path.GetFileName(dll)}: {ex.Message}"));
            }
        }
        return errors;
    }
}
