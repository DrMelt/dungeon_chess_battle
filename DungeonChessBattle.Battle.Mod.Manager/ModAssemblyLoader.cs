using System.Reflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 代码程序集加载器：以可卸载的 AssemblyLoadContext 装载 mod DLL，
/// 找到指定入口接口实现后实例化并交由调用方初始化。只做装载边界，不承担业务。
/// 入口接口经泛型指定：数据入口在 Battle.Mod.Interface 定义，展示入口在 Game.Mod.Interface 定义，本类不感知。
/// </summary>
public sealed class ModAssemblyLoader : IDisposable {
    private readonly AssemblyLoadContext _alc;
    private readonly ILogger<ModAssemblyLoader> _logger;
    private bool _loaded;

    /// <summary>装配装载上下文；mod 依赖解析先查宿主上下文（接口程序集已加载处），再回退 mod 同目录 DLL。</summary>
    /// <param name="name">ALC 名，便于诊断区分数据面与展示面上下文。</param>
    /// <param name="logger">依赖解析日志，未注入时静默。</param>
    public ModAssemblyLoader(string? name = null, ILogger<ModAssemblyLoader>? logger = null) {
        _alc = new AssemblyLoadContext(name ?? $"mod_{Guid.NewGuid():N}", isCollectible: true);
        _alc.Resolving += ResolveFallback;
        _logger = logger ?? NullLogger<ModAssemblyLoader>.Instance;
    }

    /// <summary>装载目标 DLL 并返回其首个 <typeparamref name="TEntry"/> 实现；DLL 不含入口实现返回 null。</summary>
    public TEntry? LoadEntry<TEntry>(string dllAbsolutePath) where TEntry : class {
        if (_loaded)
            throw new InvalidOperationException("该装载上下文已使用，一个上下文只装载一个 mod 程序集");
        _loaded = true;

        Assembly assembly = _alc.LoadFromAssemblyPath(Path.GetFullPath(dllAbsolutePath));
        Type[] types;
        try {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex) {
            // 依赖解析失败会成批出现：把每个失败的依赖名/原因带出，便于定位 Godot 端默认上下文缺哪些接口程序集
            string reason = string.Join(" | ",
                ex.LoaderExceptions
                    .Select(e => e is null ? "<null>" : e.Message)
                    .Distinct()
                    .Take(5));
            throw new InvalidOperationException($"程序集类型加载失败：{reason}", ex);
        }

        Type? entryType = types
            .FirstOrDefault(t => !t.IsInterface && !t.IsAbstract && typeof(TEntry).IsAssignableFrom(t));
        if (entryType is null)
            return null;
        return Activator.CreateInstance(entryType) as TEntry;
    }

    /// <inheritdoc/>
    public void Dispose() => _alc.Unload();

    private Assembly? ResolveFallback(AssemblyLoadContext context, AssemblyName name) {
        // mod 与主程序共引的接口程序集与 GodotSharp 由 Godot 运行时加载，可能分布在默认上下文与
        // 工程专用 ALC；按程序集全名匹配进程内已加载副本，避免重复类型。仅 Resolving 兜底路径调用，
        // 命中一次即被运行时缓存，无性能热点。
        Assembly? fromLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => string.Equals(a.GetName().FullName, name.FullName, StringComparison.Ordinal));
        if (fromLoaded is not null) {
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("程序集 {Assembly} 由进程内已加载副本解析", name.Name);
            return fromLoaded;
        }

        // 接口程序集之外的 mod 自带依赖，尝试在 mod 目录（已注册的目录）查找
        return ResolveFromDirectory(context, name);
    }

    private Assembly? ResolveFromDirectory(AssemblyLoadContext context, AssemblyName name) {
        string[] deps = [.. _dependencyDirectories];
        foreach (string dir in deps) {
            string candidate = Path.Combine(dir, name.Name + ".dll");
            if (!File.Exists(candidate))
                continue;
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("程序集 {Assembly} 由 mod 目录解析：{Path}", name.Name, candidate);
            return context.LoadFromAssemblyPath(candidate);
        }
        return null;
    }

    private readonly List<string> _dependencyDirectories = [];

    /// <summary>登记依赖探测目录（常为 mod 的 code 目录），供 Resolving 解析自带依赖。</summary>
    public void AddDependencyDirectory(string absolutePath) {
        if (!_dependencyDirectories.Contains(absolutePath))
            _dependencyDirectories.Add(absolutePath);
    }
}
