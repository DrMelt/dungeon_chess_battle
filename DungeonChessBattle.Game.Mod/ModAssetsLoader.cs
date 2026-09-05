using System.Text.Json;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// mod 展示资源装载器：从 mod 目录读取 godot_assets.json 并定位 images 子目录。
/// 只依赖目录路径，不依赖数据装载产物，与数据面 ModLoader 完全解耦。
/// </summary>
public static class ModAssetsLoader {
    /// <summary>godot_assets 文件名（Godot 展示资源数据）。</summary>
    public const string AssetsFileName = "godot_assets.json";

    /// <summary>mod 图标子目录名，Godot 端图标资源加载使用。</summary>
    public const string ImagesDirectoryName = "images";

    /// <summary>
    /// 读取 mod 目录的展示资源数据；godot_assets.json 缺席返回 null，解析失败抛异常。
    /// </summary>
    public static ModAssetsPackage? Load(string modDirectory) {
        string path = Path.Combine(modDirectory, AssetsFileName);
        if (!File.Exists(path))
            return null;
        var assets = JsonSerializer.Deserialize(File.ReadAllText(path), AssetsJsonContext.Default.GodotAssetsJson)
            ?? throw new InvalidOperationException($"{AssetsFileName} 解析为空");
        return new ModAssetsPackage(assets, ResolveImagesRoot(modDirectory));
    }

    /// <summary>定位 mod 目录的 images 子目录；不存在返回 null。</summary>
    public static string? ResolveImagesRoot(string modDirectory) {
        string root = Path.Combine(modDirectory, ImagesDirectoryName);
        return Directory.Exists(root) ? root : null;
    }
}

/// <summary>mod 展示资源装载结果：解析后的展示数据与图标目录。</summary>
public sealed record ModAssetsPackage(GodotAssetsJson Assets, string? ImagesRootPath);
