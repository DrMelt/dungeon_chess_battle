using System.Text.Json;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 目录加载器：扫描 mods 根目录 → 逐目录解析 manifest.json 与 content.json →
/// 缺失依赖跳过并记错误 → 依赖拓扑 + 优先级排序 → 合并内容并计算指纹。
/// 单个目录解析失败不中断其余 mod，错误以 ModLoadResult 汇总返回。
/// </summary>
public static class ModLoader {
    /// <summary>manifest 文件名。</summary>
    public const string ManifestFileName = "manifest.json";

    /// <summary>内容文件名。</summary>
    public const string ContentFileName = "content.json";

    /// <summary>代码子目录名，ALC 装载 mod 代码程序集使用。</summary>
    public const string CodeDirectoryName = "code";

    /// <summary>加载 mods 根目录下全部 mod 目录；根目录不存在返回空结果。</summary>
    public static ModLoadResult LoadDirectory(string rootPath) {
        var mods = new List<LoadedMod>();
        var errors = new List<string>();
        if (!Directory.Exists(rootPath))
            return new ModLoadResult { Mods = [], Errors = [] };

        foreach (string dir in Directory.GetDirectories(rootPath)) {
            string id = Path.GetFileName(dir);
            try {
                mods.Add(LoadModDirectory(dir));
            }
            catch (Exception ex) {
                errors.Add($"{id}: {ex.Message}");
            }
        }

        var ordered = OrderByDependency(mods, errors);
        return new ModLoadResult { Mods = ordered, Errors = errors };
    }

    /// <summary>把已排序 mod 目录合并为内容集：mod 间合并（含 BuffTypeId 段校验）+ 指纹。</summary>
    public static ContentSet BuildContentSet(IReadOnlyList<LoadedMod> orderedMods) {
        var content = ContentMerge.MergeModContent([.. orderedMods.Select(m => m.Content)]);
        string fingerprint = ContentFingerprint.Compute(orderedMods);
        return new ContentSet { Content = content, Mods = orderedMods, Fingerprint = fingerprint };
    }

    private static LoadedMod LoadModDirectory(string dir) {
        string manifestPath = Path.Combine(dir, ManifestFileName);
        string contentPath = Path.Combine(dir, ContentFileName);
        if (!File.Exists(manifestPath))
            throw new InvalidOperationException($"缺少 {ManifestFileName}");
        if (!File.Exists(contentPath))
            throw new InvalidOperationException($"缺少 {ContentFileName}");

        var manifest = JsonSerializer.Deserialize(
            File.ReadAllText(manifestPath),
            ModJsonContext.Default.ModManifestJson)
            ?? throw new InvalidOperationException($"{ManifestFileName} 解析为空");

        var content = JsonSerializer.Deserialize(
            File.ReadAllText(contentPath),
            ModJsonContext.Default.ModContentJson)
            ?? throw new InvalidOperationException($"{ContentFileName} 解析为空");

        return new LoadedMod {
            Manifest = new ModManifest(
                Id: manifest.Id,
                Name: manifest.Name,
                Version: manifest.Version,
                Revision: manifest.Revision,
                Dependencies: manifest.Dependencies,
                Priority: manifest.Priority),
            DirectoryPath = dir,
            ContentHash = ContentFingerprint.HashFile(contentPath),
            Content = content,
        };
    }

    /// <summary>依赖拓扑排序：依赖者排在被依赖者之后，同级按 Priority 升序、再按 Id 字母序，保证确定性。</summary>
    private static List<LoadedMod> OrderByDependency(IReadOnlyList<LoadedMod> mods, List<string> errors) {
        foreach (var bad in mods.Where(m => string.IsNullOrEmpty(m.Manifest.Id)))
            errors.Add($"{Path.GetFileName(bad.DirectoryPath)}: manifest.Id 不能为空");

        var unique = mods.Where(m => !string.IsNullOrEmpty(m.Manifest.Id)).ToList();
        foreach (var group in unique.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal).Where(g => g.Count() > 1))
            errors.Add(
                $"manifest.Id '{group.Key}' 重复声明：{string.Join(", ", group.Select(m => Path.GetFileName(m.DirectoryPath)))}，仅首个参与装载");

        var byId = unique.GroupBy(m => m.Manifest.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

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
                errors.Add($"{id}: 依赖成环 {string.Join(" -> ", stack.Reverse())} -> {id}");
                rejected.Add(id);
                return false;
            }

            stack.Push(id);
            foreach (var dep in mod.Manifest.Dependencies) {
                if (!byId.TryGetValue(dep, out var depMod)) {
                    errors.Add($"{id}: 依赖缺失 {dep}");
                    stack.Pop();
                    rejected.Add(id);
                    return false;
                }
                if (!Visit(depMod, stack)) {
                    stack.Pop();
                    rejected.Add(id);
                    return false;
                }
            }
            stack.Pop();

            visited.Add(id);
            result.Add(mod);
            return true;
        }
    }
}
