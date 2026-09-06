namespace DungeonChessBattle.Battle.Mod;

/// <summary>
/// mod 代码入口契约：主程序以 AssemblyLoadContext 加载 mod DLL 后，找到实现本接口的类型，
/// 实例化并调用 Initialize，在引导上下文上注册该 mod 的全部行为与内容定义。
/// mod DLL 只允许引用 Battle.Shared 与 Mod 程序集定义的契约，禁止引用引擎内部类型。
/// </summary>
public interface IModEntry {
    /// <summary>注册本 mod 的全部自定义行为与内容定义；同键重复注册以后注册者覆盖先注册者。</summary>
    void Initialize(IModBootstrapContext context);
}
