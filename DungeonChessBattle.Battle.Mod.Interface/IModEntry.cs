using DungeonChessBattle.Battle.Mod.Shared;

namespace DungeonChessBattle.Battle.Mod.Interface;

/// <summary>
/// mod 代码入口接口：主程序以 AssemblyLoadContext 加载 mod DLL 后，找到实现本接口的类型，
/// 实例化并调用 Initialize，在引导上下文上注册该 mod 的全部内容定义。
/// mod DLL 只允许引用本库与引用链传递可见的 Battle.Mod.Shared、Battle.Config.Shared、Battle.Shared，
/// 禁止引用引擎内部类型。战斗运行时对象层不对 mod 发布，权威实体写面在编译期不可见。
/// </summary>
public interface IModEntry {
    /// <summary>注册本 mod 的全部内容定义；同键重复注册以后注册者覆盖先注册者。</summary>
    void Initialize(IModBootstrapContext context);
}
