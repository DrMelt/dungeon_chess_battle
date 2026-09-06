using DungeonChessBattle.Battle.Mod;

namespace DungeonChessBattle.Game.Mod;

/// <summary>mods 根目录内单个 mod 的管理视图，供 mod 管理界面直接绑定。</summary>
public sealed class ModPackage {
    /// <summary>mod ID，同时是 mods 根目录下的子目录名。</summary>
    public required string Id {
        get; init;
    }

    /// <summary>展示名。</summary>
    public required string Name {
        get; init;
    }

    /// <summary>语义版本号。</summary>
    public required string Version {
        get; init;
    }

    /// <summary>覆盖优先级。</summary>
    public required int Priority {
        get; init;
    }

    /// <summary>是否处于启用集内。停用不改目录内容，只把它排除出装配。</summary>
    public required bool IsEnabled {
        get; init;
    }

    /// <summary>mod 目录绝对路径。</summary>
    public required string DirectoryPath {
        get; init;
    }

    /// <summary>声明的依赖 mod ID。</summary>
    public required IReadOnlyList<string> Dependencies {
        get; init;
    }

    /// <summary>是否含 code 子目录（数据代码 mod）。</summary>
    public required bool HasCode {
        get; init;
    }

    /// <summary>是否含展示代码子目录（展示代码 mod）。</summary>
    public required bool HasDisplayCode {
        get; init;
    }

    /// <summary>本 mod 的装载错误；空表示无错。</summary>
    public required IReadOnlyList<string> Errors {
        get; init;
    }

    /// <summary>未装载原因；非 null 表示该目录被拒载或解析失败，不参与装配。</summary>
    public string? Reason {
        get; init;
    }
}

/// <summary>
/// mod 管理根：扫描 mods 根目录、维护启用集、汇总装载错误与内容指纹。
/// 启用集落在 mods 目录内（<see cref="ModEnablement.FileName"/>），
/// 服务端子进程读同一目录即两端裁决一致，无需额外传参通道。
/// 启停只改启用集不改内容，装配是一次性的：变更须重启进程才生效。
/// </summary>
public sealed class ModCatalog {
    private readonly string _modsRootPath;
    private ModLoadResult _load;
    private IReadOnlyList<ModPackage> _packages;

    private ModCatalog(string modsRootPath) {
        _modsRootPath = modsRootPath;
        _load = ModLoader.LoadDirectory(modsRootPath);
        _packages = BuildPackages();
    }

    /// <summary>扫描指定 mods 根目录并建立管理视图。</summary>
    public static ModCatalog Scan(string modsRootPath) => new(modsRootPath);

    /// <summary>mods 根目录绝对路径。</summary>
    public string ModsRootPath => _modsRootPath;

    /// <summary>本次扫描的原始装载结果，供数据面直接装配，避免二次扫描。</summary>
    public ModLoadResult ScanResult => _load;

    /// <summary>参与装载的启用 mod，按依赖拓扑与优先级排序，直接交数据面装配。</summary>
    public IReadOnlyList<LoadedMod> EnabledMods => _load.Mods;

    /// <summary>全部 mods 子目录，含启用、停用与被拒载者，按 ID 字母序，供列表展示。</summary>
    public IReadOnlyList<ModPackage> Packages => _packages;

    /// <summary>因启用集而停用的 mod 数量。</summary>
    public int DisabledCount => _load.Disabled.Count;

    /// <summary>数据面装载错误。</summary>
    public IReadOnlyList<string> Errors => _load.Errors;

    /// <summary>
    /// 数据面装配期错误（代码 mod 装载失败、合并冲突回退基座），由宿主装配后追加。
    /// </summary>
    public IReadOnlyList<string> AssemblyErrors {
        get; private set;
    } = [];

    /// <summary>展示面装配错误，由 <see cref="ModAssets"/> 装配后回填。</summary>
    public IReadOnlyList<string> DisplayErrors {
        get; internal set;
    } = [];

    /// <summary>当前启用集对应的内容指纹，房间与回放门控的一致性身份。</summary>
    public string Fingerprint => ContentFingerprint.Compute(_load.Mods);

    /// <summary>
    /// 重扫 mods 目录，刷新管理视图。只影响列表与错误显示，不重装配内容——装配是一次性的。
    /// </summary>
    public void Rescan() {
        _load = ModLoader.LoadDirectory(_modsRootPath);
        _packages = BuildPackages();
    }

    /// <summary>追加宿主装配期产生的错误：扫描与排序看不到代码 mod 装载失败和内容回退基座。</summary>
    public void RecordAssemblyErrors(IEnumerable<string> errors) =>
        AssemblyErrors = [.. AssemblyErrors, .. errors];

    /// <summary>
    /// 启停一个 mod：以磁盘上的启用集为底改写该 ID 后落盘，并立即重扫使列表与磁盘一致。
    /// 指向已删目录的停用记录原样保留，否则用户删一个 mod 会顺带启回另一个。
    /// 返回 false 表示该 mod ID 不在当前扫描结果内。变更需重启进程才影响已装配内容。
    /// </summary>
    public bool SetEnabled(string modId, bool enabled) {
        if (!_packages.Any(p => p.Id == modId))
            return false;

        var disabled = ModEnablement.Load(_modsRootPath) is { } persisted
            ? persisted.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (enabled)
            disabled.Remove(modId);
        else
            disabled.Add(modId);

        ModEnablement.Save(_modsRootPath, disabled);
        Rescan();
        return true;
    }

    private List<ModPackage> BuildPackages() {
        var packages = new List<ModPackage>();
        packages.AddRange(_load.Mods.Select(mod => ToPackage(mod, enabled: true)));
        packages.AddRange(_load.Disabled.Select(mod => ToPackage(mod, enabled: false)));
        packages.AddRange(_load.Unloaded.Select(ToPackage));
        packages.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return packages;
    }

    private ModPackage ToPackage(LoadedMod mod, bool enabled) => new() {
        Id = mod.Manifest.Id,
        Name = mod.Manifest.Name,
        Version = mod.Manifest.Version,
        Priority = mod.Manifest.Priority,
        IsEnabled = enabled,
        DirectoryPath = mod.DirectoryPath,
        Dependencies = mod.Manifest.Dependencies,
        HasCode = mod.CodeHash.Length > 0,
        HasDisplayCode = HasDisplayCodeDirectory(mod.DirectoryPath),
        Errors = [.. _load.Errors.Where(error => error.StartsWith($"{mod.Manifest.Id}: ", StringComparison.Ordinal))],
    };

    /// <summary>
    /// 被拒载的目录也出一行：它没进装载列表，但用户在面板上必须看得见它和它的原因，
    /// 否则只剩一行没有归属的错误文字。
    /// </summary>
    private static ModPackage ToPackage(UnloadedMod mod) {
        string directoryName = Path.GetFileName(mod.DirectoryPath);
        return new ModPackage {
            Id = mod.Manifest?.Id ?? directoryName,
            Name = string.IsNullOrEmpty(mod.Manifest?.Name) ? directoryName : mod.Manifest!.Name,
            Version = mod.Manifest?.Version ?? "",
            Priority = mod.Manifest?.Priority ?? 0,
            IsEnabled = false,
            DirectoryPath = mod.DirectoryPath,
            Dependencies = mod.Manifest?.Dependencies ?? [],
            HasCode = Directory.Exists(Path.Combine(mod.DirectoryPath, ModLoader.CodeDirectoryName)),
            HasDisplayCode = HasDisplayCodeDirectory(mod.DirectoryPath),
            Errors = [mod.Reason],
            Reason = mod.Reason,
        };
    }

    private static bool HasDisplayCodeDirectory(string directoryPath) =>
        Directory.Exists(Path.Combine(directoryPath, ModAssets.DisplayCodeDirectoryName));
}
