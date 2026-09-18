using ErrorOr;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>
/// mod 装载的可预期失败目录：清单、启用集、路径裁决与入口装载。
/// 描述面向日志与管理面板，归属由 <see cref="ModError"/> 侧按 mod 补上；自带定位所需的路径与原因，不携带异常对象。
/// </summary>
public static class ModLoaderErrors {
    /// <summary>mod 目录根部无清单。</summary>
    public static Error ManifestMissing => Error.Validation(
        code: "ModLoader.Manifest.Missing", description: $"缺少 {ModLayout.ManifestFileName}");

    /// <summary>清单读不出来：文件被占用、权限不足或 JSON 非法。</summary>
    public static Error ManifestUnreadable(string reason) => Error.Failure(
        code: "ModLoader.Manifest.Unreadable",
        description: $"{ModLayout.ManifestFileName} 读取失败：{reason}");

    /// <summary>清单内容为空对象。</summary>
    public static Error ManifestEmpty => Error.Validation(
        code: "ModLoader.Manifest.Empty", description: $"{ModLayout.ManifestFileName} 解析为空");

    /// <summary>清单必填字段缺失。</summary>
    public static Error ManifestMissingFields(IEnumerable<string> missing) => Error.Validation(
        code: "ModLoader.Manifest.MissingField",
        description: $"{ModLayout.ManifestFileName} 缺少必填字段 {string.Join("、", missing)}；无数据代码时写 []");

    /// <summary>目录名与清单身份不一致，资源寻址按目录名执行。</summary>
    public static Error DirectoryIdMismatch(string directoryName, string id) => Error.Validation(
        code: "ModLoader.Manifest.IdMismatch",
        description: $"目录名 '{directoryName}' 与 manifest.id '{id}' 不一致，资源寻址按目录名执行，拒绝装载");

    /// <summary>清单声明的数据面产物文件不存在。</summary>
    public static Error ArtifactMissing(string fieldName, string relative) => Error.Validation(
        code: "ModLoader.Artifact.Missing",
        description: $"manifest.{fieldName} 声明的 '{relative}' 不存在，数据代码缺失即拒载");

    /// <summary>清单声明的数据面产物读不出来：文件被占用或权限不足，摘要算不出即拒载整包。</summary>
    public static Error ArtifactUnreadable(string path, string reason) => Error.Failure(
        code: "ModLoader.Artifact.Unreadable", description: $"数据面产物读取失败：{path}：{reason}");

    /// <summary>依赖探测目录列举不出来：目录被占用或权限不足，落进目录的 DLL 摘要算不出即拒载整包。</summary>
    public static Error DirectoryUnreadable(string path, string reason) => Error.Failure(
        code: "ModLoader.Directory.Unreadable", description: $"依赖探测目录列举失败：{path}：{reason}");

    /// <summary>清单声明的相对路径越出 mod 目录或语法非法。</summary>
    public static Error PathUnsafe(string fieldName, string relative) => Error.Validation(
        code: "ModLoader.Path.Unsafe",
        description: $"manifest.{fieldName} 的 '{relative}' 不是 mod 目录内的合法相对路径");

    /// <summary>启用集读不出来：文件不可读、内容非法，或解析为空对象。</summary>
    public static Error EnablementUnreadable(string path, string? reason = null) => Error.Failure(
        code: "ModLoader.Enablement.Unreadable",
        description: reason is null
            ? $"{path}：{ModLayout.EnablementFileName} 解析为空"
            : $"{path}：{reason}");

    /// <summary>启用集写入失败。</summary>
    public static Error EnablementWriteFailed(string path, string reason) => Error.Failure(
        code: "ModLoader.Enablement.WriteFailed", description: $"启用集写入失败：{path}：{reason}");

    /// <summary>DLL 内没有入口接口实现。</summary>
    public static Error EntryTypeMissing(string entryTypeName) => Error.Validation(
        code: "ModLoader.EntryType.Missing", description: $"DLL 未包含入口接口 {entryTypeName} 的实现");
}
