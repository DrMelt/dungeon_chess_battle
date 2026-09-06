using System.Text.Json;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 目录加载器：扫描 mods 根目录 → 逐目录解析 manifest.json →
/// 缺失依赖跳过并记错误 → 依赖拓扑 + 优先级排序 → 计算代码摘要指纹。
/// 单个目录解析失败不中断其余 mod，错误以 ModLoadResult 汇总返回。
/// </summary>
public static class ModLoader {
    /// <summary>manifest 文件名。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>代码子目录名，ALC 装载 mod 代码程序集使用，内容全部在代码内。</summary>
    public const string CodeDirectoryName = "code";

    /// <summary>
    /// 加载 mods 根目录下全部 mod 目录并按启用集分流；根目录不存在返回空结果。
    /// 启用集读自同目录的 <see cref="ModEnablement.FileName"/>，缺席即全部启用。
    /// </summary>
    public static ModLoadResult LoadDirectory(string rootPath) {
        var mods = new List<LoadedMod>();
        var disabled = new List<LoadedMod>();
        var unloaded = new List<UnloadedMod>();
        var errors = new List<string>();
        if (!Directory.Exists(rootPath))
            return new ModLoadResult { Mods = [], Disabled = [], Unloaded = [], Errors = [] };

        IReadOnlySet<string>? disabledIds = ModEnablement.Load(rootPath);
        foreach (string dir in Directory.GetDirectories(rootPath)) {
            string id = Path.GetFileName(dir);
            LoadedMod mod;
            try {
                mod = LoadModDirectory(dir);
            }
            catch (Exception ex) {
                errors.Add($"{id}: {ex.Message}");
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

    private static LoadedMod LoadModDirectory(string dir) {
        string manifestPath = Path.Combine(dir, ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"缺少 {ManifestFileName}");

        var manifest = JsonSerializer.Deserialize(
            File.ReadAllText(manifestPath),
            ModJsonContext.Default.ModManifestJson)
            ?? throw new InvalidOperationException($"{ManifestFileName} 解析为空");

        if (!string.Equals(Path.GetFileName(dir), manifest.Id, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"目录名 '{Path.GetFileName(dir)}' 与 manifest.Id '{manifest.Id}' 不一致，资源寻址按目录名执行，拒绝装载");

        return new LoadedMod {
            Manifest = new ModManifest(
                Id: manifest.Id,
                Name: manifest.Name,
                Version: manifest.Version,
                Revision: manifest.Revision,
                Dependencies: manifest.Dependencies,
                Priority: manifest.Priority),
            DirectoryPath = dir,
            CodeHash = ContentFingerprint.HashCodeDirectory(Path.Combine(dir, CodeDirectoryName)),
        };
    }

    /// <summary>
    /// 依赖拓扑排序：依赖者排在被依赖者之后，同级按 Priority 升序、再按 Id 字母序，保证确定性。
    /// 被拒载的 mod 一律落进 <paramref name="unloaded"/>，让管理面能列出一个都不漏。
    /// </summary>
    private static List<LoadedMod> OrderByDependency(
        IReadOnlyList<LoadedMod> mods, List<string> errors, IReadOnlyList<LoadedMod> disabled,
        List<UnloadedMod> unloaded) {
        foreach (var bad in mods.Where(m => string.IsNullOrEmpty(m.Manifest.Id))) {
            string reason = "manifest.Id 不能为空";
            errors.Add($"{Path.GetFileName(bad.DirectoryPath)}: {reason}");
            unloaded.Add(new UnloadedMod { DirectoryPath = bad.DirectoryPath, Reason = reason });
        }

        var unique = mods.Where(m => !string.IsNullOrEmpty(m.Manifest.Id)).ToList();
        foreach (var duplicate in unique.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal)
                     .Where(g => g.Count() > 1).SelectMany(g => g.Skip(1))) {
            string reason = $"manifest.Id '{duplicate.Manifest.Id}' 重复声明，仅首个参与装载";
            errors.Add($"{duplicate.Manifest.Id}: {reason}");
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

        foreach (var mod in byId.Values.OrderBy(m => m.Manifest.Priority).ThenBy(m => m.Manifest.Id, StringComparer.Ordinal)) {
            if (!Visit(mod, new Stack<string>()))
                continue;
        }
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
                    errors.Add($"{id}: {reason}");
                unloaded.Add(new UnloadedMod {
                    DirectoryPath = rejectedMod.DirectoryPath, Manifest = rejectedMod.Manifest, Reason = reason,
                });
                rejected.Add(id);
            }
        }
    }
}
