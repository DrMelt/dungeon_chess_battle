using DungeonChessBattle.Battle.Mod.Manager;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>mods 根目录内单个 mod 的管理视图，供 mod 管理界面直接绑定。</summary>
public sealed class ModEntryView {
    /// <summary>mod ID，同时是 mods 根目录下的子目录名。</summary>
    public required string Id {
        get; init;
    }

    /// <summary>语义版本号。</summary>
    public required string Version {
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

    /// <summary>是否声明了数据入口 DLL。</summary>
    public required bool HasCode {
        get; init;
    }

    /// <summary>是否声明了展示入口 DLL。</summary>
    public required bool HasDisplayCode {
        get; init;
    }

    /// <summary>本 mod 名下的装载错误；空表示无错。</summary>
    public required IReadOnlyList<ModError> Errors {
        get; init;
    }

    /// <summary>未装载原因；非 null 表示该目录被拒载或解析失败，不参与装配。</summary>
    public string? Reason {
        get; init;
    }
}

/// <summary>
/// mod 管理根：扫描 mods 根目录、读取各 mod 的展示声明、维护启用集、汇总各装配阶段的错误与内容指纹。
/// 展示声明与数据面清单是同一份 manifest.json 的两个段：数据面读顶层并裁决装载，本类读展示段并据此装配展示面。
/// 启用集落在 mods 目录内（<see cref="ModLayout.EnablementFileName"/>），
/// 服务端子进程读同一目录即两端裁决一致，无需额外传参通道。
/// 启停只改启用集不改内容，装配是一次性的：变更须重启进程才生效。
/// </summary>
public sealed class ModCatalog {
    private readonly string _modsRootPath;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<ModCatalog> _logger;
    private ModLoadResult _load;
    private Dictionary<string, ModDisplayDeclaration> _displays = new(StringComparer.Ordinal);
    private IReadOnlyList<ModDisplayDeclaration> _enabledDisplays = [];
    private IReadOnlyList<ModEntryView> _entries;

    private ModCatalog(string modsRootPath, ILoggerFactory? loggerFactory) {
        _modsRootPath = modsRootPath;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ModCatalog>() ?? NullLogger<ModCatalog>.Instance;
        _load = ModLoader.LoadDirectory(modsRootPath, loggerFactory);
        RefreshDisplays();
        _entries = BuildEntries();
    }

    /// <summary>扫描指定 mods 根目录并建立管理视图。</summary>
    public static ModCatalog Scan(string modsRootPath, ILoggerFactory? loggerFactory = null) =>
        new(modsRootPath, loggerFactory);

    /// <summary>本次扫描的原始装载结果，供数据面直接装配，避免二次扫描。</summary>
    public ModLoadResult ScanResult => _load;

    /// <summary>参与装载的启用 mod，按依赖拓扑排序，直接交数据面装配。</summary>
    public IReadOnlyList<LoadedMod> EnabledMods => _load.Mods;

    /// <summary>启用 mod 的展示面声明，顺序与 <see cref="EnabledMods"/> 一致，交展示装配消费。</summary>
    public IReadOnlyList<ModDisplayDeclaration> EnabledDisplays => _enabledDisplays;

    /// <summary>全部 mods 子目录，含启用、停用与被拒载者，按 ID 字母序，供列表展示。</summary>
    public IReadOnlyList<ModEntryView> Entries => _entries;

    /// <summary>因启用集而停用的 mod 数量。</summary>
    public int DisabledCount => _load.Disabled.Count;

    /// <summary>扫描期错误：清单、依赖与启用集裁决的结果。</summary>
    public IReadOnlyList<ModError> Errors => _load.Errors;

    /// <summary>根目录级问题：未提供目录、目录不存在、启用集不可读；非 null 时本次未装载任何 mod。</summary>
    public string? RootProblem => _load.RootProblem;

    /// <summary>数据面装配期错误：数据代码入口装载失败，由宿主装配后追加。</summary>
    public IReadOnlyList<ModError> AssemblyErrors {
        get; private set;
    } = [];

    /// <summary>展示面装配期错误：展示代码装载失败与展示键引用不成立，由 <see cref="ModAssets.Assemble"/> 装配后追加。</summary>
    public IReadOnlyList<ModError> DisplayErrors {
        get; private set;
    } = [];

    /// <summary>展示面声明读取错误：展示段写错、缺字段或路径非法。每次扫描重算，不影响数据面装载。</summary>
    public IReadOnlyList<ModError> DisplayDeclarationErrors {
        get; private set;
    } = [];

    /// <summary>当前启用集对应的内容指纹，房间与回放门控的一致性身份。</summary>
    public string Fingerprint => ContentFingerprint.Compute(_load.Mods);

    /// <summary>
    /// 重扫 mods 目录，刷新管理视图。只影响列表与错误显示，不重装配内容——装配是一次性的。
    /// </summary>
    public void Rescan() {
        _load = ModLoader.LoadDirectory(_modsRootPath, _loggerFactory);
        RefreshDisplays();
        _entries = BuildEntries();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("重扫 mod 目录：启用 {Enabled} 个，停用 {Disabled} 个，拒载 {Rejected} 个",
                _load.Mods.Count, _load.Disabled.Count, _load.Unloaded.Count);
    }

    /// <summary>追加数据面装配期错误：扫描与排序看不到代码 mod 装载失败。</summary>
    public void RecordAssemblyErrors(IEnumerable<ModError> errors) =>
        AssemblyErrors = [.. AssemblyErrors, .. errors];

    /// <summary>追加展示面装配期错误：扫描与数据装配都看不到展示代码的问题。</summary>
    public void RecordDisplayErrors(IEnumerable<ModError> errors) =>
        DisplayErrors = [.. DisplayErrors, .. errors];

    /// <summary>
    /// 启停一个 mod：以磁盘上的启用集为底改写该 ID 后落盘，并立即重扫使列表与磁盘一致。
    /// 指向已删目录的停用记录原样保留，否则用户删一个 mod 会顺带启回另一个。
    /// 返回 false 表示该 mod ID 不在当前扫描结果内。变更需重启进程才影响已装配内容。
    /// </summary>
    public bool SetEnabled(string modId, bool enabled) {
        if (!_entries.Any(p => p.Id == modId)) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("{ModId} 不在当前扫描结果内，启停未落盘", modId);
            return false;
        }

        var disabled = ModEnablement.Load(_modsRootPath) is { } persisted
            ? persisted.ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (enabled)
            disabled.Remove(modId);
        else
            disabled.Add(modId);

        ModEnablement.Save(_modsRootPath, disabled, _logger);
        Rescan();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("mod {ModId} 已{Action}，装配是一次性的，重启进程后生效",
                modId, enabled ? "启用" : "停用");
        return true;
    }

    private List<ModEntryView> BuildEntries() {
        var entries = new List<ModEntryView>();
        entries.AddRange(_load.Mods.Select(mod => ToEntryView(mod, enabled: true)));
        entries.AddRange(_load.Disabled.Select(mod => ToEntryView(mod, enabled: false)));
        entries.AddRange(_load.Unloaded.Select(ToEntryView));
        entries.Sort((left, right) => string.CompareOrdinal(left.Id, right.Id));
        return entries;
    }

    private ModEntryView ToEntryView(LoadedMod mod, bool enabled) => new() {
        Id = mod.Manifest.Id,
        Version = mod.Manifest.Version,
        IsEnabled = enabled,
        DirectoryPath = mod.DirectoryPath,
        Dependencies = mod.Manifest.Dependencies,
        HasCode = mod.Manifest.Code.Count > 0,
        HasDisplayCode = HasDisplayCodeOf(mod.Manifest.Id),
        Errors = [.. _load.Errors.Where(error => error.ModId == mod.Manifest.Id)],
    };

    /// <summary>
    /// 被拒载的目录也出一行：它没进装载列表，但用户在面板上必须看得见它和它的原因，
    /// 否则只剩一行没有归属的错误文字。
    /// </summary>
    private ModEntryView ToEntryView(UnloadedMod mod) {
        string directoryName = Path.GetFileName(mod.DirectoryPath);
        string id = mod.Manifest?.Id ?? directoryName;
        var error = _load.Errors.FirstOrDefault(e => e.ModId == id) ?? new ModError(id, mod.Reason);
        return new ModEntryView {
            Id = id,
            Version = mod.Manifest?.Version ?? "",
            IsEnabled = false,
            DirectoryPath = mod.DirectoryPath,
            Dependencies = mod.Manifest?.Dependencies ?? [],
            HasCode = mod.Manifest?.Code.Count > 0,
            // 被拒载的目录不读展示声明：它连数据面内容都没进来，展示更无从谈起
            HasDisplayCode = false,
            Errors = [error],
            Reason = mod.Reason,
        };
    }

    /// <summary>
    /// 读全部参与扫描的 mod 的展示声明。停用者也读：它不进装配，但列表要显示有没有展示代码。
    /// 展示声明只影响展示面，读取失败不进数据面的拒载裁决。
    /// </summary>
    private void RefreshDisplays() {
        var errors = new List<ModError>();
        var displays = new Dictionary<string, ModDisplayDeclaration>(StringComparer.Ordinal);
        foreach (var mod in _load.Mods.Concat(_load.Disabled))
            displays[mod.Manifest.Id] = ModDisplayDeclarationReader.Read(mod, errors);

        _displays = displays;
        _enabledDisplays = [.. _load.Mods.Select(mod => displays[mod.Manifest.Id])];
        DisplayDeclarationErrors = errors;
        if (_logger.IsEnabled(LogLevel.Error))
            foreach (var error in errors)
                _logger.LogError("展示声明读取失败：{ModId}：{Reason}", error.ModId, error.Message);
    }

    private bool HasDisplayCodeOf(string modId) =>
        _displays.TryGetValue(modId, out var display) && display.EntryDlls.Count > 0;
}
