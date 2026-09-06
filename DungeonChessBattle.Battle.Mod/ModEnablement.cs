using System.Text.Json;

namespace DungeonChessBattle.Battle.Mod;

/// <summary>mods.enabled.json 文件结构，camelCase 键。</summary>
public sealed class ModEnablementJson {
    /// <summary>被停用的 mod ID 列表；缺席即全部启用。</summary>
    public List<string> Disabled { get; set; } = [];
}

/// <summary>
/// mods 根目录内的启用集：记录被停用的 mod ID。
/// 放在 mods 目录内而非别处，是因为服务端子进程与客户端读同一 mods 根目录，
/// 启停裁决无需扩参数通道即两端一致，停用集合变化会联动内容指纹。
/// </summary>
public static class ModEnablement {
    /// <summary>启用集文件名，位于 mods 根目录。</summary>
    public const string FileName = "mods.enabled.json";

    /// <summary>读取启用集；文件缺席或根目录不存在返回 null，等价于全部启用。解析失败抛异常。</summary>
    public static IReadOnlySet<string>? Load(string rootPath) {
        string path = Path.Combine(rootPath, FileName);
        if (!File.Exists(path))
            return null;
        var data = JsonSerializer.Deserialize(File.ReadAllText(path), ModJsonContext.Default.ModEnablementJson)
            ?? throw new InvalidOperationException($"{FileName} 解析为空");
        return data.Disabled.ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>写入启用集，只落被停用的 ID 并按字母序，保证文件内容对同一状态稳定。</summary>
    public static void Save(string rootPath, IReadOnlyCollection<string> disabledIds) {
        Directory.CreateDirectory(rootPath);
        var data = new ModEnablementJson {
            Disabled = [.. disabledIds.Distinct(StringComparer.Ordinal).OrderBy(id => id, StringComparer.Ordinal)],
        };
        File.WriteAllText(Path.Combine(rootPath, FileName),
            JsonSerializer.Serialize(data, ModJsonContext.Default.ModEnablementJson));
    }
}
