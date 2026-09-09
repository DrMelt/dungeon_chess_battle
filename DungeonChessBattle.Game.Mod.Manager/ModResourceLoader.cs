using DungeonChessBattle.Battle.Mod.Manager;
using DungeonChessBattle.Game.Mod.Shared;
using Godot;

namespace DungeonChessBattle.Game.Mod.Manager;

/// <summary>
/// mod 包内展示资源的加载实现：图片与场景按 <see cref="ModAssetKey"/> 寻址并缓存，加载失败一律返回 null，不抛。
/// </summary>
/// <remarks>
/// 资源可来自 mods 目录内的裸文件，也可来自经 <c>ProjectSettings.LoadResourcePack</c> 挂载的 PCK
/// （Godot 端装配时逐 mod 挂载它声明的资源包）。两类来源共用同一份相对寻址：
/// 裸文件走绝对路径直读（<see cref="Image.LoadFromFile"/>），PCK 走挂载后的 res:// 路径（<see cref="GD.Load"/>）。
/// 合法性判定共用 <see cref="ModRelativePath"/>：mod 声明的 <c>../</c> 之类越界路径直接拒绝。
/// </remarks>
public sealed class ModResourceLoader(string modsRootPath, string? modsRootGodotPath = null) : IModResourceLoader {
    private readonly string _modsRoot = Path.GetFullPath(modsRootPath);
    private readonly Dictionary<ModAssetKey, Texture2D?> _textures = [];
    private readonly Dictionary<ModAssetKey, PackedScene?> _scenes = [];
    private readonly Dictionary<ModAssetKey, Resource?> _resources = [];

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

        // 裸文件不存在时回退到 PCK 挂载路径读取；两者同为相对寻址，同一个键二选一
        if (texture is null && modsRootGodotPath is { } root && TryGetGodotPath(root, in key, out string? godotPath)) {
            try {
                texture = GD.Load<Texture2D>(godotPath);
            }
            catch (Exception) {
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
        if (modsRootGodotPath is { } root && TryGetGodotPath(root, in key, out string? godotPath)) {
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

    /// <inheritdoc/>
    public T? LoadResource<T>(in ModAssetKey key) where T : Resource {
        if (_resources.TryGetValue(key, out Resource? cached))
            return cached as T;

        // .tres 必须经 Godot 资源系统加载，只走 PCK 挂载后的 res:// 路径；裸文件 .tres 视为不存在
        Resource? resource = null;
        if (modsRootGodotPath is { } root && TryGetGodotPath(root, in key, out string? godotPath)) {
            try {
                resource = GD.Load<T>(godotPath);
            }
            catch (Exception) {
                // mod 展示数据损坏只丢该条目，展示回退由消费方处理
                resource = null;
            }
        }

        _resources[key] = resource;
        return resource as T;
    }

    /// <summary>把寻址解析为 mods 根目录内的绝对路径；越界或文件缺失返回 false。</summary>
    public bool TryGetPath(in ModAssetKey key, out string? absolutePath) {
        absolutePath = null;
        if (!ModRelativePath.IsSafe(key.ModId))
            return false;
        if (!ModRelativePath.TryResolve(Path.Combine(_modsRoot, key.ModId), key.RelativePath, out absolutePath))
            return false;
        if (!File.Exists(absolutePath))
            absolutePath = null;
        return absolutePath is not null;
    }

    /// <summary>把同一寻址解析为 Godot 路径体系下的加载路径；越界或根路径为空返回 false。</summary>
    private static bool TryGetGodotPath(string root, in ModAssetKey key, out string? godotPath) {
        godotPath = null;
        if (!IsContained(key))
            return false;

        godotPath = $"{root.TrimEnd('/')}/{key.ModId}/{key.RelativePath}";
        return true;
    }

    /// <summary>mod 声明的相对路径必须是 mod 目录名与其内路径两段都合法，规则与 manifest 声明同源。</summary>
    private static bool IsContained(in ModAssetKey key) =>
        ModRelativePath.IsSafe(key.ModId) && ModRelativePath.IsSafe(key.RelativePath);
}
