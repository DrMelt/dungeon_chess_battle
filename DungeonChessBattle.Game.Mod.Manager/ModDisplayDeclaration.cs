using System.Text.Json;
using System.Text.Json.Serialization;
using DungeonChessBattle.Battle.Mod.Manager;
using ErrorOr;

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
}

/// <summary>
/// 展示面声明读取器：从 manifest.json 的展示段读出本 mod 的展示产物并定位成绝对路径。
/// 与数据面各读一次同一份清单，互不传递：数据面只登记段的存在，段内容在这里解释，
/// 定位规则复用 <see cref="ModRelativePath"/> 这一道安全闸。
/// 声明不可用以错误返回，不拒载——展示面缺席不该影响两端的装载集合与内容指纹。
/// </summary>
public static class ModDisplayDeclarationReader {
    /// <summary>
    /// 读一个 mod 的展示声明。段缺席即空声明；清单读不出来、段写错或缺字段以错误返回，调用方据此跳过该 mod 的展示面；
    /// 段内单个条目非法或缺失不使声明整体失败，逐条记 <paramref name="errors"/> 后其余条目照常可用。
    /// </summary>
    /// <param name="mod">已通过数据面校验的 mod，提供 ID、目录与清单路径。</param>
    /// <param name="errors">非致命问题的落点，条目带归属 mod ID。</param>
    public static ErrorOr<ModDisplayDeclaration> Read(LoadedMod mod, List<ModError> errors) {
        string modId = mod.Manifest.Id;
        string directory = mod.DirectoryPath;

        var section = ReadSection(directory);
        if (section.IsError)
            return section.FirstError;

        if (section.Value is not { } json)
            return Empty(modId, directory);

        if (MissingRequired(json) is { Count: > 0 } missing)
            return ModDisplayErrors.SectionMissingFields(missing);

        var entries = ResolveFiles(directory, modId, json.Code!, "code", errors);
        var packs = ResolveFiles(directory, modId, json.Packs!, "packs", errors);
        var directories = ResolveProbeDirectories(directory, modId, json.CodeLibraries, entries, errors);
        return new ModDisplayDeclaration {
            ModId = modId,
            DirectoryPath = directory,
            EntryDlls = entries,
            ProbeDirectories = directories,
            ResourcePacks = packs,
        };
    }

    /// <summary>
    /// 读展示段并反序列化。清单读不出来、段存在却解释不了都以错误返回；段缺席返回空值，表示该 mod 无展示面。
    /// </summary>
    private static ErrorOr<ModDisplayJson?> ReadSection(string directory) {
        string text;
        try {
            text = File.ReadAllText(ModLayout.ManifestOf(directory));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return ModDisplayErrors.ManifestUnreadable(ex.Message);
        }

        try {
            using var document = JsonDocument.Parse(text);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty(ModLayout.ManifestDisplaySection, out JsonElement section))
                return (ModDisplayJson?)null;
            ModDisplayJson? parsed = section.Deserialize(ModDisplayJsonContext.Default.ModDisplayJson);
            if (parsed is null)
                return ModDisplayErrors.SectionUnreadable("段不是对象");
            return parsed;
        }
        catch (JsonException ex) {
            return ModDisplayErrors.SectionUnreadable(ex.Message);
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

    /// <summary>段缺席的空声明：该 mod 不贡献展示面。</summary>
    private static ModDisplayDeclaration Empty(string modId, string directory) => new() {
        ModId = modId,
        DirectoryPath = directory,
        EntryDlls = [],
        ProbeDirectories = [],
        ResourcePacks = [],
    };
}
