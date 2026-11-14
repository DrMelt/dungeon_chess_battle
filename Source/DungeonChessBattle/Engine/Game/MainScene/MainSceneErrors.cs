using ErrorOr;

namespace DungeonChessBattle.Game.MainScene;

/// <summary>
/// 组装场景加载的可预期失败：同类组装场景已在场，或该组装场景资源未装配。
/// 描述自带场景归属，面向调用方提示，不携带异常对象。
/// </summary>
public static class MainSceneErrors {
    /// <summary>同类组装场景已在进行中，不重复加载。</summary>
    public static Error AssemblyBusy(string assemblyName) => Error.Conflict(
        code: "MainScene.Assembly.Busy", description: $"{assemblyName}已在进行中，未重复加载");

    /// <summary>组装场景资源未装配，无法实例化。</summary>
    public static Error AssemblySceneMissing(string assemblyName) => Error.Failure(
        code: "MainScene.Assembly.SceneMissing", description: $"{assemblyName}组装场景未装配");
}
