using System.Text.Json;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 目录加载器：扫描 mods 根目录 → 逐目录解析 manifest.json → 把声明的相对路径定位成 mod 目录内的绝对路径 →
/// 缺失依赖跳过并记错误 → 依赖拓扑 + 优先级排序 → 计算数据代码摘要指纹。
/// 路径的合法性、回落与拒载裁决全在这里一次做完，下游只消费 <see cref="LoadedMod"/> 上的绝对路径。
/// 单个目录解析失败不中断其余 mod，错误以 ModLoadResult 汇总返回。
/// 清单文件名与默认目录名见 <see cref="ModLayout"/>，产物本身落在哪里由 manifest 声明。
/// </summary>
public static class ModLoader {
    /// <summary>
    /// 加载 mods 根目录下全部 mod 目录并按启用集分流；根目录不存在返回空结果。
    /// 启用集读自同目录的 <see cref="ModLayout.EnablementFileName"/>，缺席即全部启用。
    /// </summary>
    public static ModLoadResult LoadDirectory(string rootPath) {
        var mods = new List<LoadedMod>();
        var disabled = new List<LoadedMod>();
        var unloaded = new List<UnloadedMod>();
        var errors = new List<ModError>();
        if (!Directory.Exists(rootPath))
            return new ModLoadResult { Mods = [], Disabled = [], Unloaded = [], Errors = [] };

        IReadOnlySet<string>? disabledIds = ModEnablement.Load(rootPath);
        foreach (string dir in Directory.GetDirectories(rootPath)) {
            string id = Path.GetFileName(dir);
            LoadedMod mod;
            try {
                mod = LoadModDirectory(dir, errors);
            }
            catch (Exception ex) {
                errors.Add(new ModError(id, ex.Message));
                unloaded.Add(new UnloadedMod { DirectoryPath = dir, Reason = ex.Message });
                continue;
            }

            if (disabledIds is not null && disabledIds.Contains(mod.Manifest.Id))
                disabled.Add(mod);
            else
                mods.Add(mod);
        }

        var ordered = OrderByDependency(mods, errors, disabled, unloaded);
        return new ModLoadResult { Mods = ordered, Disabled = disabled, Unloaded = unloaded, Errors = errors };
    }

    private static LoadedMod LoadModDirectory(string dir, List<ModError> errors) {
        string manifestPath = ModLayout.ManifestOf(dir);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"缺少 {ModLayout.ManifestFileName}");

        var manifest = JsonSerializer.Deserialize(
            File.ReadAllText(manifestPath),
            ModJsonContext.Default.ModManifestJson)
            ?? throw new InvalidOperationException($"{ModLayout.ManifestFileName} 解析为空");

        if (!string.Equals(Path.GetFileName(dir), manifest.Id, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"目录名 '{Path.GetFileName(dir)}' 与 manifest.Id '{manifest.Id}' 不一致，资源寻址按目录名执行，拒绝装载");

        var codeEntries = ResolveArtifacts(dir, manifest.Code, ModLayout.CodeDirectoryName,
            "code", rejectWhenMissing: true);
        var displayEntries = ResolveArtifacts(dir, manifest.CodeDisplay, ModLayout.DisplayCodeDirectoryName,
            "codeDisplay", rejectWhenMissing: false);
        var packages = ResolveArtifacts(dir, manifest.Packages, ModLayout.AssetsDirectoryName,
            "packages", rejectWhenMissing: false, searchPattern: "*.pck");

        var codeLibraries = ResolveProbeDirectories(dir, manifest.CodeLibraries, codeEntries,
            "codeLibraries", reportMissing: true, errors);
        var displayLibraries = ResolveProbeDirectories(dir, manifest.CodeDisplayLibraries, displayEntries,
            "codeDisplayLibraries", reportMissing: false, errors);

        return new LoadedMod {
            Manifest = new ModManifest(
                Id: manifest.Id,
                Name: manifest.Name,
                Version: manifest.Version,
                Revision: manifest.Revision,
                Dependencies: manifest.Dependencies,
                Priority: manifest.Priority,
                Code: ToDeclared(dir, codeEntries),
                CodeLibraries: manifest.CodeLibraries ?? [],
                CodeDisplay: ToDeclared(dir, displayEntries),
                CodeDisplayLibraries: manifest.CodeDisplayLibraries ?? [],
                Packages: ToDeclared(dir, packages)),
            DirectoryPath = dir,
            CodeEntries = codeEntries,
            CodeLibraries = codeLibraries,
            DisplayEntries = displayEntries,
            DisplayLibraries = displayLibraries,
            Packages = packages,
            CodeHash = ContentFingerprint.HashCodeFiles(codeEntries, codeLibraries),
        };
    }

    /// <summary>
    /// 把 manifest 声明的产物文件定位成绝对路径并保持声明顺序；未声明即按默认目录名枚举其内匹配文件，
    /// 按文件名 Ordinal 排序以保证两端同序——<c>Directory.GetFiles</c> 的返回顺序不作保证。
    /// 声明即承诺存在：数据面产物缺失整包拒载（包不完整），展示面产物缺失只留装载期错误——
    /// 只有客户端目录里才有展示产物，让它参与拒载裁决会让两端的装载集合分叉，<c>DataRevision</c> 即不一致。
    /// </summary>
    private static List<string> ResolveArtifacts(
        string modDirectory, List<string>? declared, string defaultDirectoryName, string fieldName,
        bool rejectWhenMissing, string searchPattern = "*.dll") {
        if (declared is null)
            return [.. EnumerateDefault(modDirectory, defaultDirectoryName, searchPattern)];

        var resolved = new List<string>(declared.Count);
        foreach (string relative in declared) {
            string path = ResolveWithinModDirectory(modDirectory, relative, fieldName);
            if (rejectWhenMissing && !File.Exists(path))
                throw new InvalidOperationException(
                    $"manifest.{fieldName} 声明的 '{relative}' 不存在，数据代码缺失即拒载");
            resolved.Add(path);
        }

        return resolved;
    }

    /// <summary>
    /// 汇总该面的依赖探测目录：manifest 声明的目录 + 各入口文件自身所在目录，按绝对路径去重。
    /// 声明的探测目录缺席时，数据面记一条错误（声明与包不一致），展示面静默跳过（只有客户端判得到）。
    /// </summary>
    private static List<string> ResolveProbeDirectories(
        string modDirectory, List<string>? declared, IReadOnlyList<string> entries, string fieldName,
        bool reportMissing, List<ModError> errors) {
        var directories = new List<string>();
        foreach (string relative in declared ?? []) {
            string path = ResolveWithinModDirectory(modDirectory, relative, fieldName);
            if (!Directory.Exists(path)) {
                if (reportMissing)
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

    /// <summary>枚举默认目录内匹配的文件；目录缺席即该面无产物。</summary>
    private static IEnumerable<string> EnumerateDefault(
        string modDirectory, string defaultDirectoryName, string searchPattern) {
        string directory = Path.Combine(modDirectory, defaultDirectoryName);
        if (!Directory.Exists(directory))
            return [];
        return Directory.GetFiles(directory, searchPattern, SearchOption.TopDirectoryOnly)
            .OrderBy(Path.GetFileName, StringComparer.Ordinal);
    }

    /// <summary>把相对声明解析为 mod 目录内的绝对路径；越界或非法即拒载——两端读同一份清单，语法裁决必然一致。</summary>
    private static string ResolveWithinModDirectory(string modDirectory, string relative, string fieldName) =>
        ModRelativePath.TryResolve(modDirectory, relative, out string? path) && path is not null
            ? path
            : throw new InvalidOperationException(
                $"manifest.{fieldName} 的 '{relative}' 不是 mod 目录内的合法相对路径");

    /// <summary>把绝对路径集还原为相对 mod 目录的 <c>/</c> 分隔声明，供清单与管理面展示。</summary>
    private static List<string> ToDeclared(string modDirectory, IReadOnlyList<string> absolutePaths) =>
        [.. absolutePaths.Select(path =>
            Path.GetRelativePath(modDirectory, path).Replace(Path.DirectorySeparatorChar, '/'))];

    private static void AddDistinct(List<string> directories, string path) {
        if (!directories.Contains(path, StringComparer.Ordinal))
            directories.Add(path);
    }

    /// <summary>
    /// 依赖拓扑排序：依赖者排在被依赖者之后，同级按 Priority 升序、再按 Id 字母序，保证确定性。
    /// 被拒载的 mod 一律落进 <paramref name="unloaded"/>，让管理面能列出一个都不漏。
    /// </summary>
    private static List<LoadedMod> OrderByDependency(
        IReadOnlyList<LoadedMod> mods, List<ModError> errors, IReadOnlyList<LoadedMod> disabled,
        List<UnloadedMod> unloaded) {
        foreach (string badDirectory in mods
                 .Where(m => string.IsNullOrEmpty(m.Manifest.Id))
                 .Select(m => m.DirectoryPath)) {
            string reason = "manifest.Id 不能为空";
            errors.Add(new ModError(Path.GetFileName(badDirectory), reason));
            unloaded.Add(new UnloadedMod { DirectoryPath = badDirectory, Reason = reason });
        }

        var unique = mods.Where(m => !string.IsNullOrEmpty(m.Manifest.Id)).ToList();
        foreach (var duplicate in unique.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1).SelectMany(g => g.Skip(1))) {
            string reason = $"manifest.Id '{duplicate.Manifest.Id}' 重复声明，仅首个参与装载";
            errors.Add(new ModError(duplicate.Manifest.Id, reason));
            unloaded.Add(new UnloadedMod {
                DirectoryPath = duplicate.DirectoryPath, Manifest = duplicate.Manifest, Reason = reason,
            });
        }

        var byId = unique.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var disabledIds = disabled.Select(m => m.Manifest.Id).ToHashSet(StringComparer.Ordinal);

        var result = new List<LoadedMod>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var rejected = new HashSet<string>(StringComparer.Ordinal);

        foreach (var mod in byId.Values.OrderBy(m => m.Manifest.Priority).ThenBy(m => m.Manifest.Id, StringComparer.Ordinal))
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
                    // 被停用的依赖与被删掉的依赖是两件事，UI 侧要能据此提示用户去开回上游
                    Reject(mod, disabledIds.Contains(dep) ? $"依赖已停用 {dep}" : $"依赖缺失 {dep}");
                    stack.Pop();
                    return false;
                }
                if (!Visit(depMod, stack)) {
                    // 原因已由被依赖者自己报出，这里只登记连带拒载，不重复报错
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
                unloaded.Add(new UnloadedMod {
                    DirectoryPath = rejectedMod.DirectoryPath, Manifest = rejectedMod.Manifest, Reason = reason,
                });
                rejected.Add(id);
            }
        }
    }
}
