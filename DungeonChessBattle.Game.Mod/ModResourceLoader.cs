using DungeonChessBattle.Game.Shared;
using Godot;

namespace DungeonChessBattle.Game.Mod;

/// <summary>
/// mod 包内展示资源的加载实现：图片与场景按 <see cref="ModAssetKey"/> 寻址并缓存，加载失败一律返回 null，不抛。
/// </summary>
/// <remarks>
/// 图片走绝对路径直读（<see cref="Image.LoadFromFile"/> 不经资源系统），场景必须经
/// <see cref="GD.Load"/>，故宿主需告知 mods 根目录在 Godot 路径体系下的挂载点；未告知即不支持 mod 自带场景。
/// 寻址一律先归一化并校验仍在根目录内，mod 声明的 <c>../</c> 之类越界路径直接拒绝。
/// 引擎内置资源不经本类，由宿主直接注册进展示注册表。
/// </remarks>
public sealed class ModResourceLoader(string modsRootPath, string? modsRootGodotPath = null) : IModResourceLoader {
    private readonly string _modsRoot = Path.GetFullPath(modsRootPath);
    private readonly Dictionary<ModAssetKey, Texture2D?> _textures = [];
    private readonly Dictionary<ModAssetKey, PackedScene?> _scenes = [];

    /// <inheritdoc/>
    public Texture2D? LoadTexture(in ModAssetKey key) {
        if (_textures.TryGetValue(key, out Texture2D? cached))
            return cached;

        Texture2D? texture = null;
        if (TryGetPath(in key, out string? path)) {
            try {
                var image = Image.LoadFromFile(path);
                if (image is not null)
                    texture = ImageTexture.CreateFromImage(image);
            }
            catch (Exception) {
                // 图片损坏只丢该图，展示回退由消费方处理
                texture = null;
            }
        }

        _textures[key] = texture;
        return texture;
    }

    /// <inheritdoc/>
    public PackedScene? LoadScene(in ModAssetKey key) {
        if (_scenes.TryGetValue(key, out PackedScene? cached))
            return cached;

        PackedScene? scene = null;
        // mod 自带场景只在宿主声明了引擎路径挂载点时可加载
        if (modsRootGodotPath is not null && TryGetGodotPath(in key, out string? godotPath)) {
            try {
                scene = GD.Load<PackedScene>(godotPath);
            }
            catch (Exception) {
                // mod 自带场景损坏只丢该场景，展示回退由消费方处理
                scene = null;
            }
        }

        _scenes[key] = scene;
        return scene;
    }

    /// <summary>把寻址解析为 mods 根目录内的绝对路径；越界或文件缺失返回 false。</summary>
    public bool TryGetPath(in ModAssetKey key, out string? absolutePath) {
        absolutePath = null;
        if (!IsContained(key))
            return false;

        string candidate = Path.GetFullPath(
            Path.Combine(_modsRoot, key.ModId, key.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(_modsRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;
        if (!File.Exists(candidate))
            return false;

        absolutePath = candidate;
        return true;
    }

    /// <summary>把同一寻址解析为 Godot 路径体系下的加载路径；未声明挂载点或越界返回 false。</summary>
    private bool TryGetGodotPath(in ModAssetKey key, out string? godotPath) {
        godotPath = null;
        if (!IsContained(key))
            return false;

        godotPath = $"{modsRootGodotPath!.TrimEnd('/')}/{key.ModId}/{key.RelativePath}";
        return true;
    }

    /// <summary>mod 声明的相对路径必须是非空、不含上级跳转、不以分隔符开头的包内路径。</summary>
    private static bool IsContained(in ModAssetKey key) {
        if (string.IsNullOrEmpty(key.ModId) || string.IsNullOrEmpty(key.RelativePath))
            return false;
        if (key.RelativePath.StartsWith('/') || key.RelativePath.Contains(".."))
            return false;
        return true;
    }
}
