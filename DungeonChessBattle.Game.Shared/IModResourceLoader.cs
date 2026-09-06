using Godot;

namespace DungeonChessBattle.Game.Shared;

/// <summary>
/// mod 与引擎两侧展示资源的统一加载面：把展示数据声明的图标与场景引用解析成 Godot 对象。
/// 契约在本库，实现在 Game.Mod；解析失败一律返回 null，由消费方按无展示数据处理。
/// 引擎内置资源不走本接口——由宿主以资源名直接向 <see cref="IModDisplayRuntime"/> 注册，与 mod 资源同一名空间。
/// </summary>
public interface IModResourceLoader {
    /// <summary>加载 mod 包内图片为纹理；文件缺失或解码失败返回 null。实现侧缓存。</summary>
    Texture2D? LoadTexture(in ModAssetKey key);

    /// <summary>加载 mod 包内场景为模板；文件缺失或解析失败返回 null。实现侧缓存。</summary>
    PackedScene? LoadScene(in ModAssetKey key);
}
