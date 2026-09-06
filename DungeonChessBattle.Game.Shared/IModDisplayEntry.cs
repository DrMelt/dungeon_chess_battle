namespace DungeonChessBattle.Game.Shared;

/// <summary>
/// mod 展示代码入口契约：客户端以 AssemblyLoadContext 加载展示 DLL 后，找到实现本接口的类型，
/// 实例化并调用 Initialize，把该 mod 的展示资源与视图注册进展示注册表。
/// 展示 DLL 允许引用 Game.Shared 与 Godot 类型，仅客户端装载，服务端不加载、不进指纹。
/// </summary>
public interface IModDisplayEntry {
    /// <summary>注册本 mod 的全部展示资源与视图；同键后注册覆盖先注册者，未声明字段沿用内置。</summary>
    void Initialize(IModDisplayRuntime runtime, ModDisplayContext context);
}

/// <summary>mod 展示装配上下文：本 mod 身份与包内资源加载器。</summary>
/// <param name="ModId">mod 唯一 ID，即 mods 根目录下的目录名，ModAssetKey 寻址前缀。</param>
/// <param name="Resources">把包内相对路径解析为 Godot 图片/场景的加载器，仅限本 mod 目录。</param>
public readonly record struct ModDisplayContext(string ModId, IModResourceLoader Resources);
