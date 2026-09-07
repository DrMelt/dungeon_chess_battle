namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 代码入口装载器：遍历 mod，定位其某个代码子目录，逐个 DLL 以独立 ALC 找到
/// 指定的入口接口实现并交回调初始化。两端与展示面共用这一段边界，
/// 装载器不感知入口契约本身，也不知道注册了什么。
/// 子目录缺席即该 mod 不贡献这类代码，静默跳过；单个 DLL 失败记一条 <see cref="ModError"/> 并继续其余。
/// </summary>
public static class ModEntryLoader {
    /// <summary>
    /// 逐 mod 装载 <paramref name="subDirectoryName"/> 下的入口程序集，实例化后交 <paramref name="initialize"/> 初始化。
    /// </summary>
    /// <param name="mods">参与装载的 mod，顺序即装载顺序。</param>
    /// <param name="subDirectoryName">mod 目录内的代码子目录名，见 <see cref="ModLayout"/>。</param>
    /// <param name="alcNamePrefix">ALC 名前缀，便于诊断区分数据面与展示面上下文。</param>
    /// <param name="failureText">失败条目的原因前缀，如「数据代码入口装载失败」。</param>
    /// <param name="initialize">入口实例与该 mod 的配对回调，注册动作在其中发生。</param>
    public static List<ModError> LoadEntries<TEntry>(
        IReadOnlyList<LoadedMod> mods,
        string subDirectoryName,
        string alcNamePrefix,
        string failureText,
        Action<TEntry, LoadedMod> initialize) where TEntry : class {
        var errors = new List<ModError>();
        foreach (var mod in mods) {
            string codeDir = Path.Combine(mod.DirectoryPath, subDirectoryName);
            if (!Directory.Exists(codeDir))
                continue;

            foreach (string dll in Directory.GetFiles(codeDir, "*.dll", SearchOption.TopDirectoryOnly)) {
                try {
                    using var loader = new ModAssemblyLoader($"{alcNamePrefix}{mod.Manifest.Id}");
                    loader.AddDependencyDirectory(codeDir);
                    var entry = loader.LoadEntry<TEntry>(dll);
                    if (entry is not null)
                        initialize(entry, mod);
                }
                catch (Exception ex) {
                    errors.Add(new ModError(
                        mod.Manifest.Id, $"{failureText} {Path.GetFileName(dll)}: {ex.Message}"));
                }
            }
            // Initialize 注册的委托强引用本 ALC 内的类型，Dispose 的 Unload 不会真正回收程序集；
            // 实例照常可调用，代价是当前单次装配模型不支持 mod 热重载
        }
        return errors;
    }
}
