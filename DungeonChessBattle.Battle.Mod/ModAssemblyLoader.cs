using System.Reflection;
using System.Runtime.Loader;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 代码程序集加载器：以可卸载的 AssemblyLoadContext 装载 mod DLL，
/// 找到指定入口接口实现后实例化并交由调用方初始化。只做装载边界，不承担业务。
/// 入口接口经泛型指定：数据入口在 Battle.Mod 定义，展示入口在 Game.Shared 定义，本类不感知。
/// </summary>
public sealed class ModAssemblyLoader : IDisposable {
    private readonly AssemblyLoadContext _alc;
    private bool _loaded;

    /// <summary>装配装载上下文；mod 依赖解析先查默认上下文（契约程序集在其中），再回退 mod 同目录 DLL。</summary>
    public ModAssemblyLoader(string? name = null) {
        _alc = new AssemblyLoadContext(name ?? $"mod_{Guid.NewGuid():N}", isCollectible: true);
        _alc.Resolving += ResolveFallback;
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
            types = [.. ex.Types.OfType<Type>()];
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
        // mod 与主程序共引的契约程序集已加载于默认上下文，直出主副本避免重复类型
        Assembly? fromDefault = AssemblyLoadContext.Default.Assemblies
            .FirstOrDefault(a => string.Equals(a.GetName().FullName, name.FullName, StringComparison.Ordinal));
        if (fromDefault is not null)
            return fromDefault;

        // 契约之外的 mod 自带依赖，尝试在 mod 目录（已注册的目录）查找
        return ResolveFromDirectory(context, name);
    }

    private Assembly? ResolveFromDirectory(AssemblyLoadContext context, AssemblyName name) {
        string[] deps = [.. _dependencyDirectories];
        foreach (string dir in deps) {
            string candidate = Path.Combine(dir, name.Name + ".dll");
            if (File.Exists(candidate))
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
