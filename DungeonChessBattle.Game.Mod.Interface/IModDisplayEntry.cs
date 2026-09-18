using DungeonChessBattle.Game.Display.Registry;
using DungeonChessBattle.Game.Mod.Shared;

namespace DungeonChessBattle.Game.Mod.Interface;

/// <summary>
/// 展示入口接口：客户端以 AssemblyLoadContext 加载展示 DLL 后，找到实现本接口的类型，实例化并调用 Initialize，
/// 把该 mod 的全部展示数据注册进展示注册表。展示 DLL 仅客户端装载，服务端不加载、不进指纹。
/// </summary>
public interface IModDisplayEntry {
    /// <summary>注册本 mod 的全部展示数据；同键后注册覆盖先注册者。</summary>
    void Initialize(IDisplayRegistrar registrar, ModDisplayContext context);
}
