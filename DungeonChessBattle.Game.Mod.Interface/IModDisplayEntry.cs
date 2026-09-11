using DungeonChessBattle.Game.Mod.Shared;

namespace DungeonChessBattle.Game.Mod.Interface;

/// <summary>
/// mod 展示代码入口契约：客户端以 AssemblyLoadContext 加载展示 DLL 后，找到实现本接口的类型，
/// 实例化并调用 Initialize，把该 mod 的展示数据注册进展示注册表。
/// 本库是 mod 开发唯一的引用锚点，且只放 mod 要实现的接口：注册表定义与装配上下文在 <c>Game.Mod.Shared</c>、
/// 共用的展示数据在 <c>Game.Shared</c>，两者经本库的引用链传递可见。
/// 包内资源由展示代码按 <c>res://mods/{mod id}/</c> 前缀自行加载，宿主只递 mod ID。
/// 展示 DLL 仅客户端装载，服务端不加载、不进指纹。
/// </summary>
public interface IModDisplayEntry {
    /// <summary>注册本 mod 的全部展示资源与展示数据；同键后注册覆盖先注册者，未声明字段沿用被覆盖者。</summary>
    void Initialize(IModDisplayRuntime runtime, ModDisplayContext context);
}
