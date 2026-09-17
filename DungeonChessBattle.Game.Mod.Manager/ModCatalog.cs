using System.Diagnostics.CodeAnalysis;
using DungeonChessBattle.Battle.Mod.Manager;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// mod 管理根：扫描 mods 根目录、读取各 mod 的展示声明、维护启用集、汇总各装配阶段的错误与内容指纹。
/// 展示声明与数据面清单是同一份 manifest.json 的两个段：数据面读顶层并裁决装载，本库读展示段并据此装配展示面。
/// 启用集落在 mods 目录内（<see cref="ModLayout.EnablementFileName"/>），
/// 服务端子进程读同一目录即两端裁决一致，无需额外传参通道。
/// 启停只改启用集不改内容，装配是一次性的：变更须重启进程才生效。
/// </summary>
public sealed class ModCatalog {
    private readonly string _modsRootPath;
    private readonly ILoggerFactory? _loggerFactory;
    private readonly ILogger<ModCatalog> _logger;
    private ModLoadResult _load;
    private ModDisplaySet _displays;
    private IReadOnlyList<ModEntryView> _entries;

    private ModCatalog(string modsRootPath, ILoggerFactory? loggerFactory) {
        _modsRootPath = modsRootPath;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory?.CreateLogger<ModCatalog>() ?? NullLogger<ModCatalog>.Instance;
        Refresh();
    }

    /// <summary>扫描指定 mods 根目录并建立管理视图。</summary>
    public static ModCatalog Scan(string modsRootPath, ILoggerFactory? loggerFactory = null) =>
        new(modsRootPath, loggerFactory);

    /// <summary>本次扫描的原始装载结果，供数据面直接装配，避免二次扫描。</summary>
    public ModLoadResult ScanResult => _load;

    /// <summary>参与装载的启用 mod，按依赖拓扑排序，直接交数据面装配。</summary>
    public IReadOnlyList<LoadedMod> EnabledMods => _load.Mods;

    /// <summary>
    /// 启用 mod 中声明可用的展示面声明，顺序与 <see cref="EnabledMods"/> 一致，交展示装配消费；
    /// 声明不可用者不进此列，原因见 <see cref="ModDisplayErrors"/>。
    /// </summary>
    public IReadOnlyList<ModDisplayDeclaration> EnabledDisplays => _displays.Enabled;

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
    public IReadOnlyList<ModError> DisplayDeclarationErrors => _displays.DeclarationErrors;

    /// <summary>当前启用集对应的内容指纹，房间与回放门控的一致性身份。</summary>
    public string Fingerprint => ContentFingerprint.Compute(_load.Mods);

    /// <summary>
    /// 重扫 mods 目录，刷新管理视图。只影响列表与错误显示，不重装配内容——装配是一次性的。
    /// </summary>
    public void Rescan() {
        Refresh();
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
    /// 失败以错误返回，只影响本次启停，列表按旧状态继续。变更需重启进程才影响已装配内容。
    /// </summary>
    public ErrorOr<Success> SetEnabled(string modId, bool enabled) {
        if (!_entries.Any(p => p.Id == modId)) {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("{ModId} 不在当前扫描结果内，启停未落盘", modId);
            return ModCatalogErrors.NotInScan(modId);
        }

        var persisted = ModEnablement.Load(_modsRootPath, _logger);
        if (persisted.IsError) {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError("启用集不可读，启停未落盘：{Reason}", persisted.FirstError.Description);
            return persisted.FirstError;
        }

        var disabled = persisted.Value.ToHashSet(StringComparer.Ordinal);
        if (enabled)
            disabled.Remove(modId);
        else
            disabled.Add(modId);

        var saved = ModEnablement.Save(_modsRootPath, disabled, _logger);
        if (saved.IsError) {
            if (_logger.IsEnabled(LogLevel.Error))
                _logger.LogError("启用集写入失败，启停未落盘：{Reason}", saved.FirstError.Description);
            return saved.FirstError;
        }

        Rescan();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("mod {ModId} 已{Action}，装配是一次性的，重启进程后生效",
                modId, enabled ? "启用" : "停用");
        return Result.Success;
    }

    /// <summary>
    /// 刷新一次扫描的三项结果：装载目录、读展示声明、建条目列表。
    /// 三步必须同时生效，否则列表会拿旧声明判新装载结果有没有展示代码。
    /// </summary>
    [MemberNotNull(nameof(_load), nameof(_displays), nameof(_entries))]
    private void Refresh() {
        _load = ModLoader.LoadDirectory(_modsRootPath, _loggerFactory);
        _displays = ModDisplaySet.Read(_load, _logger);
        _entries = ModEntryViewBuilder.Build(_load, _displays);
    }
}
