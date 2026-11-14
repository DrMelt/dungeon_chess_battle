using System.Text.Json;
using ErrorOr;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Mod.Manager;

/// <summary>mods.enabled.json 文件结构，camelCase 键。</summary>
internal sealed class ModEnablementJson {
    /// <summary>被停用的 mod ID 列表；缺席即全部启用。</summary>
    public List<string> Disabled { get; set; } = [];
}

/// <summary>
/// mods 根目录内的启用集：记录被停用的 mod ID，文件名见 <see cref="ModLayout.EnablementFileName"/>。
/// 服务端子进程与客户端读同一 mods 根目录，启停裁决两端一致；停用集合变化会联动内容指纹。
/// </summary>
public static class ModEnablement {
    /// <summary>读取启用集；文件缺席或根目录不存在即无停用项，等价于全部启用。不可读以错误返回。</summary>
    public static ErrorOr<IReadOnlySet<string>> Load(string rootPath, ILogger? logger = null) {
        string path = Path.Combine(rootPath, ModLayout.EnablementFileName);
        if (!File.Exists(path)) {
            if (logger is not null && logger.IsEnabled(LogLevel.Debug))
                logger.LogDebug("无启用集文件，全部 mod 启用：{Path}", path);
            return new HashSet<string>(StringComparer.Ordinal);
        }

        ModEnablementJson? data;
        try {
            data = JsonSerializer.Deserialize(File.ReadAllText(path), ModJsonContext.Default.ModEnablementJson);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException) {
            return ModLoaderErrors.EnablementUnreadable(path, ex.Message);
        }

        if (data is null)
            return ModLoaderErrors.EnablementUnreadable(path);
        if (logger is not null && logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("读取启用集：{Path}，停用 {Count} 个", path, data.Disabled.Count);
        return data.Disabled.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>写入启用集，只落被停用的 ID 并按字母序，保证文件内容对同一状态稳定。写入失败以错误返回。</summary>
    public static ErrorOr<Success> Save(
        string rootPath, IReadOnlyCollection<string> disabledIds, ILogger? logger = null) {
        string path = Path.Combine(rootPath, ModLayout.EnablementFileName);
        var data = new ModEnablementJson {
            Disabled = [.. disabledIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)],
        };
        try {
            Directory.CreateDirectory(rootPath);
            File.WriteAllText(path, JsonSerializer.Serialize(data, ModJsonContext.Default.ModEnablementJson));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) {
            return ModLoaderErrors.EnablementWriteFailed(path, ex.Message);
        }

        if (logger is not null && logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("写入启用集：{Path}，停用 {Count} 个", path, data.Disabled.Count);
        return Result.Success;
    }
}
