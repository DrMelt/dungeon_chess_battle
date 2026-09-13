using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonChessBattle.Battle.Mod.Manager;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// manifest.json 中展示面声明段的结构，camelCase 键，未知键与缺失必填字段一律判为声明不可用。
/// 段名见 <see cref="ModLayout.ManifestDisplaySection"/>；数据面只登记该键存在，段内容由本类解释。
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class ModDisplayJson {
    /// <summary>展示入口 DLL，相对 mod 目录，数组顺序即装载顺序；必填，无展示代码时写 <c>[]</c>。</summary>
    public List<string>? Code {
        get; set;
    }

    /// <summary>展示面额外的依赖探测目录；入口 DLL 所在目录自动附加，依赖与入口同目录时无需声明。</summary>
    public List<string>? CodeLibraries {
        get; set;
    }

    /// <summary>待挂载的展示资源包，相对 mod 目录；必填，无展示资源时写 <c>[]</c>。</summary>
    public List<string>? Packs {
        get; set;
    }
}

/// <summary>
/// 一个 mod 的展示面声明：展示入口、依赖探测目录与待挂载资源包，路径已定位为 mod 目录内绝对路径。
/// 声明段缺席即空声明，表示该 mod 不贡献展示面。
/// <see cref="Problem"/> 非 null 表示声明不可用，展示装配整体跳过该 mod；数据面装载与内容指纹都不受影响。
/// </summary>
public sealed class ModDisplayDeclaration {
    /// <summary>mod ID，与清单 id 及目录名一致。</summary>
    public required string ModId {
        get; init;
    }

    /// <summary>mod 所在绝对目录。</summary>
    public required string DirectoryPath {
        get; init;
    }

    /// <summary>展示入口 DLL 绝对路径，顺序即装载顺序；空表示无展示代码。</summary>
    public required IReadOnlyList<string> EntryDlls {
        get; init;
    }

    /// <summary>展示面依赖探测目录绝对路径，含各入口自身所在目录。</summary>
    public required IReadOnlyList<string> ProbeDirectories {
        get; init;
    }

    /// <summary>待挂载的展示资源包绝对路径；空表示无展示资源。</summary>
    public required IReadOnlyList<string> ResourcePacks {
        get; init;
    }

    /// <summary>声明不可用的原因；非 null 表示该 mod 的展示面整体跳过。</summary>
    public string? Problem {
        get; init;
    }
}

/// <summary>
/// 展示面声明读取器：从 manifest.json 的展示段读出本 mod 的展示产物并定位成绝对路径。
/// 与数据面各读一次同一份清单，互不传递：数据面只登记段的存在，段内容在这里解释，
/// 定位规则复用 <see cref="ModRelativePath"/> 这一道安全闸。
/// 问题一律记 <see cref="ModError"/> 而不拒载——展示面缺席不该影响两端的装载集合与内容指纹。
/// </summary>
public static class ModDisplayDeclarationReader {
    /// <summary>
    /// 读一个 mod 的展示声明。段缺席即空声明；段写错、字段缺失或路径非法时记错误并返回带
    /// <see cref="ModDisplayDeclaration.Problem"/> 的空声明，调用方据此跳过该 mod 的展示面。
    /// </summary>
    /// <param name="mod">已通过数据面校验的 mod，提供 ID、目录与清单路径。</param>
    /// <param name="errors">错误落点，条目带归属 mod ID。</param>
    public static ModDisplayDeclaration Read(LoadedMod mod, List<ModError> errors) {
        string modId = mod.Manifest.Id;
        string directory = mod.DirectoryPath;

        (ModDisplayJson? json, string? problem) = ReadSection(modId, directory, errors);
        if (problem is not null) {
            errors.Add(new ModError(modId, problem));
            return Empty(modId, directory, problem);
        }

        if (json is null)
            return Empty(modId, directory, problem: null);

        string section = ModLayout.ManifestDisplaySection;
        if (MissingRequired(json) is { Count: > 0 } missing) {
            string reason = $"manifest.{section} 缺少必填字段 {string.Join("、", missing)}；无该产物时写 []";
            errors.Add(new ModError(modId, reason));
            return Empty(modId, directory, reason);
        }

        var entries = ResolveFiles(directory, modId, json.Code!, "code", errors);
        var packs = ResolveFiles(directory, modId, json.Packs!, "packs", errors);
        var directories = ResolveProbeDirectories(directory, modId, json.CodeLibraries, entries, errors);
        return new ModDisplayDeclaration {
            ModId = modId,
            DirectoryPath = directory,
            EntryDlls = entries,
            ProbeDirectories = directories,
            ResourcePacks = packs,
            Problem = null,
        };
    }

    /// <summary>
    /// 读展示段并反序列化。段缺席与清单读不出来都返回空且无原因，读不出来时另记一条错误；
    /// 段存在却解释不了返回空且带原因，由调用方判为声明不可用。
    /// </summary>
    private static (ModDisplayJson? Json, string? Problem) ReadSection(
        string modId, string directory, List<ModError> errors) {
        string manifestPath = ModLayout.ManifestOf(directory);
        string text;
        try {
            text = File.ReadAllText(manifestPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            errors.Add(new ModError(modId, $"{ModLayout.ManifestFileName} 展示段读取失败：{ex.Message}"));
            return (null, null);
        }

        try {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(ModLayout.ManifestDisplaySection, out JsonElement section))
                return (null, null);
            var parsed = section.Deserialize(ModDisplayJsonContext.Default.ModDisplayJson);
            return parsed is null
                ? (null, $"manifest.{ModLayout.ManifestDisplaySection} 段不是对象，展示面未装配")
                : (parsed, null);
        }
        catch (JsonException ex) {
            return (null, $"manifest.{ModLayout.ManifestDisplaySection} 段不可解析，展示面未装配：{ex.Message}");
        }
    }

    private static List<string>? MissingRequired(ModDisplayJson json) {
        List<string> missing = [];
        if (json.Code is null)
            missing.Add("code");
        if (json.Packs is null)
            missing.Add("packs");
        return missing.Count > 0 ? missing : null;
    }

    /// <summary>定位一段声明的产物文件；非法或越界的条目跳过并记错误，其余条目照常可用。</summary>
    private static List<string> ResolveFiles(
        string directory, string modId, List<string> declared, string fieldName, List<ModError> errors) {
        var resolved = new List<string>(declared.Count);
        foreach (string relative in declared) {
            if (ModRelativePath.TryResolve(directory, relative, out string? path) && path is not null) {
                resolved.Add(path);
                continue;
            }

            errors.Add(new ModError(modId,
                $"manifest.{ModLayout.ManifestDisplaySection}.{fieldName} 的 '{relative}' 不是 mod 目录内的合法相对路径"));
        }

        return resolved;
    }

    /// <summary>
    /// 汇总展示面的依赖探测目录：清单声明的目录 + 各入口文件自身所在目录，按绝对路径去重。
    /// 声明的探测目录缺席即记错误——声明与包不一致要看得出来，缺了它入口自带的依赖解析不到。
    /// </summary>
    private static List<string> ResolveProbeDirectories(
        string directory, string modId, IReadOnlyList<string>? declared, IReadOnlyList<string> entries,
        List<ModError> errors) {
        var directories = new List<string>();
        foreach (string relative in declared ?? []) {
            if (!ModRelativePath.TryResolve(directory, relative, out string? path) || path is null) {
                errors.Add(new ModError(modId,
                    $"manifest.{ModLayout.ManifestDisplaySection}.codeLibraries 的 '{relative}' 不是 mod 目录内的合法相对路径"));
                continue;
            }

            if (!Directory.Exists(path)) {
                errors.Add(new ModError(modId,
                    $"manifest.{ModLayout.ManifestDisplaySection}.codeLibraries 声明的 '{relative}' 不存在"));
                continue;
            }

            AddDistinct(directories, path);
        }

        foreach (string entry in entries)
            AddDistinct(directories, Path.GetDirectoryName(entry)!);

        return directories;
    }

    private static void AddDistinct(List<string> directories, string path) {
        if (!directories.Contains(path, StringComparer.Ordinal))
            directories.Add(path);
    }

    private static ModDisplayDeclaration Empty(string modId, string directory, string? problem) => new() {
        ModId = modId,
        DirectoryPath = directory,
        EntryDlls = [],
        ProbeDirectories = [],
        ResourcePacks = [],
        Problem = problem,
    };
}
