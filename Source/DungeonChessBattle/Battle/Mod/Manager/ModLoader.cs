using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 目录装载：扫描 mods 根目录，校验清单并把数据面声明的相对路径定位成绝对路径，
/// 依赖缺失或成环者拒载并按依赖顺序排列其余 mod。只处理数据面，展示面声明段的解释归 Game.Mod.Manager。
/// 根级问题不装载任何 mod，仅由 <see cref="ModLoadResult.RootProblem"/> 承载。
/// </summary>
public static class ModLoader {
    /// <summary>
    /// 加载 mods 根目录下全部 mod 目录并按启用集分流；根目录不可用返回空结果，原因在 <see cref="ModLoadResult.RootProblem"/>。
    /// 逐 mod 明细记 Debug，拒载与解析失败记 Error，连带拒载在 <see cref="OrderByDependency"/> 记 Warning。
    /// </summary>
    public static ModLoadResult LoadDirectory(string rootPath, ILoggerFactory? loggerFactory = null) {
        var logger = loggerFactory?.CreateLogger(typeof(ModLoader).FullName!) ?? NullLogger.Instance;
        if (string.IsNullOrEmpty(rootPath)) {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("未提供 mod 目录，本次不装载任何 mod");
            return EmptyRoot("未提供 mod 目录");
        }

        if (!Directory.Exists(rootPath)) {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("mods 目录不存在，本次不装载任何 mod：{Root}", rootPath);
            return EmptyRoot($"mods 目录不存在：{rootPath}");
        }

        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("扫描 mod 目录：{Root}", rootPath);

        string[] directories;
        try {
            directories = Directory.GetDirectories(rootPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            // 目录枚举失败属根级问题：记根级原因并不装载任何 mod，不中止装配；
            // 原因另以异常连栈落一条日志，根级问题文案本身不含异常对象
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning(ex, "mods 目录枚举失败，本次不装载任何 mod：{Root}", rootPath);
            return EmptyRoot($"mods 目录枚举失败：{ex.Message}");
        }

        var enablement = ModEnablement.Load(rootPath, logger);
        if (enablement.IsError) {
            // 启用集读取失败即不装载任何 mod：不静默、也不阻断进程启动，两端按同一裁决得到同一内容
            string problem = enablement.FirstError.Description;
            if (logger.IsEnabled(LogLevel.Error))
                logger.LogError("启用集不可读，本次不装载任何 mod：{Reason}", problem);
            return new ModLoadResult {
                Mods = [],
                Disabled = [],
                Errors = [],
                RootProblem = $"启用集不可读，本次未装载任何 mod：{problem}",
                Unloaded = [.. directories.Select(dir => new UnloadedMod {
                    DirectoryPath = dir, Reason = "启用集不可读，本次不装载",
                })],
            };
        }

        IReadOnlySet<string> disabledIds = enablement.Value;
        var mods = new List<LoadedMod>();
        var disabled = new List<LoadedMod>();
        var unloaded = new List<UnloadedMod>();
        var errors = new List<ModError>();
        foreach (string dir in directories) {
            string id = Path.GetFileName(dir);
            // 清单与产物的可预期失败都已在 LoadModDirectory 内收成错误，此处不捕获异常
            ErrorOr<LoadedMod> parsed = LoadModDirectory(dir, errors);
            if (parsed.IsError) {
                string reason = parsed.FirstError.Description;
                errors.Add(new ModError(id, reason));
                unloaded.Add(new UnloadedMod { DirectoryPath = dir, Reason = reason });
                continue;
            }

            LoadedMod mod = parsed.Value;
            if (disabledIds.Contains(mod.Manifest.Id)) {
                disabled.Add(mod);
                if (logger.IsEnabled(LogLevel.Debug))
                    logger.LogDebug("跳过已停用的 mod {ModId}", mod.Manifest.Id);
            }
            else {
                mods.Add(mod);
                LogLoaded(logger, mod);
            }
        }

        var ordered = OrderByDependency(mods, errors, disabled, unloaded, logger);
        // 错误由各裁决点记进 errors，在此集中落日志一次，避免同一事实两处输出
        foreach (var error in errors)
            LogLoadFailed(logger, error);
        if (logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("mod 扫描完成：启用 {Enabled} 个，停用 {Disabled} 个，拒载 {Rejected} 个",
                ordered.Count, disabled.Count, unloaded.Count);
        return new ModLoadResult {
            Mods = ordered, Disabled = disabled, Unloaded = unloaded, Errors = errors, RootProblem = null,
        };
    }

    /// <summary>根级问题下的空结果：所有目录均未装载，原因只在 RootProblem，逐目录错误不重复报。</summary>
    private static ModLoadResult EmptyRoot(string problem) =>
        new() {
            Mods = [], Disabled = [], Unloaded = [], Errors = [], RootProblem = problem
        };

    private static ErrorOr<LoadedMod> LoadModDirectory(string dir, List<ModError> errors) {
        string manifestPath = ModLayout.ManifestOf(dir);
        if (!File.Exists(manifestPath))
            return ModLoaderErrors.ManifestMissing;

        ModManifestJson? manifest;
        try {
            manifest = JsonSerializer.Deserialize(
                File.ReadAllText(manifestPath),
                ModJsonContext.Default.ModManifestJson);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            // 坏 JSON 与读取失败的文件由序列化器与文件系统抛出，在本库的裁决点收成错误
            return ModLoaderErrors.ManifestUnreadable(ex.Message);
        }
        if (manifest is null)
            return ModLoaderErrors.ManifestEmpty;

        ErrorOr<Success> validated = ValidateRequired(manifest);
        if (validated.IsError)
            return validated.FirstError;

        // 必填字段已校验，此处起按非空消费
        string id = manifest.Id!;
        if (!string.Equals(Path.GetFileName(dir), id, StringComparison.Ordinal))
            return ModLoaderErrors.DirectoryIdMismatch(Path.GetFileName(dir), id);

        ErrorOr<List<string>> codeEntries = ResolveArtifacts(dir, manifest.Code!, "code");
        if (codeEntries.IsError)
            return codeEntries.FirstError;

        ErrorOr<List<string>> codeLibraries = ResolveProbeDirectories(
            dir, manifest.CodeLibraries, codeEntries.Value, "codeLibraries", errors);
        if (codeLibraries.IsError)
            return codeLibraries.FirstError;

        ErrorOr<string> codeHash = ContentFingerprint.HashCodeFiles(codeEntries.Value, codeLibraries.Value);
        if (codeHash.IsError)
            return codeHash.FirstError;

        return new LoadedMod {
            Manifest = new ModManifest(
                Id: id,
                Version: manifest.Version!,
                Revision: manifest.Revision!,
                Dependencies: manifest.Dependencies,
                Code: ToDeclared(dir, codeEntries.Value)),
            DirectoryPath = dir,
            CodeEntries = codeEntries.Value,
            CodeLibraries = codeLibraries.Value,
            CodeHash = codeHash.Value,
        };
    }

    /// <summary>
    /// 数据面必填字段校验。身份与版本字段进指纹，产物字段决定装载什么：缺席即拒载，不接受默认值。
    /// 展示面字段不在此列：它们归 Game.Mod.Manager 校验，写错也不该影响两端的装载集合。
    /// </summary>
    private static ErrorOr<Success> ValidateRequired(ModManifestJson manifest) {
        List<string> missing = [];
        if (string.IsNullOrEmpty(manifest.Id))
            missing.Add("id");
        if (string.IsNullOrEmpty(manifest.Version))
            missing.Add("version");
        if (string.IsNullOrEmpty(manifest.Revision))
            missing.Add("revision");
        if (manifest.Code is null)
            missing.Add("code");
        if (missing.Count > 0)
            return ModLoaderErrors.ManifestMissingFields(missing);
        return Result.Success;
    }

    /// <summary>
    /// 把数据面声明的产物文件定位成绝对路径并保持声明顺序；产物字段必填，故没有未声明即回落的分支。
    /// 声明即承诺存在：数据面产物缺失整包拒载，两端内容必须同源。
    /// </summary>
    private static ErrorOr<List<string>> ResolveArtifacts(string modDirectory, List<string> declared,
        string fieldName) {
        var resolved = new List<string>(declared.Count);
        foreach (string relative in declared) {
            ErrorOr<string> path = ResolveWithinModDirectory(modDirectory, relative, fieldName);
            if (path.IsError)
                return path.FirstError;
            if (!File.Exists(path.Value))
                return ModLoaderErrors.ArtifactMissing(fieldName, relative);
            resolved.Add(path.Value);
        }

        return resolved;
    }

    /// <summary>
    /// 汇总数据面的依赖探测目录：清单声明的目录与各入口文件自身所在目录，按绝对路径去重。
    /// 声明的探测目录缺席记错误并继续其余，清单声明的路径非法整包拒载。
    /// </summary>
    private static ErrorOr<List<string>> ResolveProbeDirectories(
        string modDirectory, List<string>? declared, IReadOnlyList<string> entries, string fieldName,
        List<ModError> errors) {
        var directories = new List<string>();
        foreach (string relative in declared ?? []) {
            ErrorOr<string> resolved = ResolveWithinModDirectory(modDirectory, relative, fieldName);
            if (resolved.IsError)
                return resolved.FirstError;
            string path = resolved.Value;
            if (!Directory.Exists(path)) {
                errors.Add(new ModError(Path.GetFileName(modDirectory),
                    $"manifest.{fieldName} 声明的 '{relative}' 不存在"));
                continue;
            }

            AddDistinct(directories, path);
        }

        foreach (string entry in entries)
            AddDistinct(directories, Path.GetDirectoryName(entry)!);

        return directories;
    }

    /// <summary>把相对声明解析为 mod 目录内的绝对路径；越界或语法非法返回错误——两端读同一份清单，语法裁决必然一致。</summary>
    private static ErrorOr<string> ResolveWithinModDirectory(string modDirectory, string relative, string fieldName) =>
        ModRelativePath.TryResolve(modDirectory, relative, out string? path) && path is not null
            ? path
            : ModLoaderErrors.PathUnsafe(fieldName, relative);

    /// <summary>把绝对路径集还原为相对 mod 目录的 <c>/</c> 分隔声明，供清单与管理面展示。</summary>
    private static List<string> ToDeclared(string modDirectory, IReadOnlyList<string> absolutePaths) =>
        [.. absolutePaths.Select(path =>
            Path.GetRelativePath(modDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))];

    private static void AddDistinct(List<string> directories, string path) {
        if (!directories.Contains(path, StringComparer.Ordinal))
            directories.Add(path);
    }

    /// <summary>
    /// 依赖拓扑排序：依赖者排在被依赖者之后，其余按 Id 字母序，保证确定性。
    /// 被拒载的 mod 一律落进 <paramref name="unloaded"/>，让管理面完整列出被拒载者。
    /// </summary>
    private static List<LoadedMod> OrderByDependency(
        IReadOnlyList<LoadedMod> mods, List<ModError> errors, IReadOnlyList<LoadedMod> disabled,
        List<UnloadedMod> unloaded, ILogger logger) {
        var byId = mods.ToDictionary(m => m.Manifest.Id, StringComparer.Ordinal);
        var disabledIds = disabled.Select(m => m.Manifest.Id).ToHashSet(StringComparer.Ordinal);

        var result = new List<LoadedMod>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mod in byId.Values.OrderBy(m => m.Manifest.Id, StringComparer.Ordinal))
            Visit(mod, new Stack<string>());
        return result;

        bool Visit(LoadedMod mod, Stack<string> stack) {
            string id = mod.Manifest.Id;
            if (visited.Contains(id))
                return true;
            if (rejected.Contains(id))
                return false;
            if (stack.Contains(id)) {
                Reject(mod, $"依赖成环 {string.Join(" -> ", stack.Reverse())} -> {id}");
                return false;
            }

            stack.Push(id);
            foreach (var dep in mod.Manifest.Dependencies) {
                if (!byId.TryGetValue(dep, out var depMod)) {
                    // 被停用的依赖与被删掉的依赖是两件事，管理面据此提示用户启用上游
                    Reject(mod, disabledIds.Contains(dep) ? $"依赖已停用 {dep}" : $"依赖缺失 {dep}");
                    stack.Pop();
                    return false;
                }
                if (!Visit(depMod, stack)) {
                    // 原因已由被依赖者报出，这里只登记连带拒载
                    Reject(mod, $"依赖未装载 {dep}", report: false);
                    stack.Pop();
                    return false;
                }
            }
            stack.Pop();

            visited.Add(id);
            result.Add(mod);
            return true;

            void Reject(LoadedMod rejectedMod, string reason, bool report = true) {
                if (report)
                    errors.Add(new ModError(id, reason));
                else if (logger.IsEnabled(LogLevel.Warning))
                    // 连带原因已在上游报出，日志仍留目录行便于追溯
                    logger.LogWarning("mod 未装载：{ModId}：{Reason}", id, reason);
                unloaded.Add(new UnloadedMod {
                    DirectoryPath = rejectedMod.DirectoryPath, Manifest = rejectedMod.Manifest, Reason = reason,
                });
                rejected.Add(id);
            }
        }
    }

    #region 日志

    /// <summary>逐 mod 目录解析明细：身份、依赖、数据入口数量与数据代码摘要前缀。</summary>
    private static void LogLoaded(ILogger logger, LoadedMod mod) {
        if (!logger.IsEnabled(LogLevel.Debug))
            return;
        logger.LogDebug(
            "解析 mod {ModId} v{Version} rev{Revision} 依赖 [{Dependencies}] 数据入口 {CodeCount} 个 codeHash {CodeHash}",
            mod.Manifest.Id, mod.Manifest.Version, mod.Manifest.Revision,
            string.Join(", ", mod.Manifest.Dependencies), mod.CodeEntries.Count, HashPrefix(mod.CodeHash));
    }

    private static void LogLoadFailed(ILogger logger, ModError error) {
        if (logger.IsEnabled(LogLevel.Error))
            logger.LogError("mod 装载失败：{ModId}：{Reason}", error.ModId, error.Message);
    }

    /// <summary>摘要前 8 位；无代码即无摘要，以「-」占位以免误读为截断。</summary>
    private static string HashPrefix(string hash) => hash.Length switch {
        0 => "-",
        <= 8 => hash,
        _ => hash[..8],
    };

    #endregion
}
