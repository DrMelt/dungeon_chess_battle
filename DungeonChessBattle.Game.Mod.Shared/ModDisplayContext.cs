namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// mod 展示装配上下文：宿主递给展示代码入口的一切读数。
/// <see cref="Registry"/> 是注册表的实时只读视图，mod 据此改写已声明的条目而不必先验其内容。
/// </summary>
/// <param name="ModId">mod 唯一 ID，即 mods 根目录下的目录名，ModAssetKey 寻址前缀。</param>
/// <param name="Resources">把包内相对路径解析为 Godot 图片/场景的加载器，仅限本 mod 目录。</param>
/// <param name="Registry">展示注册表的只读查询面，含引擎预置条目与先装载 mod 的条目。</param>
public readonly record struct ModDisplayContext(
    string ModId,
    IModResourceLoader Resources,
    IDisplayRegistry Registry);
