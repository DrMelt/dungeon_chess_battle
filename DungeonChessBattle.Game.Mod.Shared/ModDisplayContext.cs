namespace DungeonChessBattle.Game.Mod.Shared;

/// <summary>
/// mod 展示装配上下文：宿主递给展示代码入口的一切读数。
/// <see cref="Registry"/> 是注册表的实时视图，mod 据此改写已声明的条目而不必先验其内容。
/// 包内资源不经本上下文传递：mod 自行以 <c>GD.Load</c> 读 <c>res://mods/{mod id}/</c> 下的文件，
/// 该前缀在 mod 导出资源包时就已固化，宿主只保证入口执行前包已挂载。
/// </summary>
/// <param name="ModId">mod 唯一 ID，即 mods 根目录下的目录名，也是包内资源寻址的目录段。</param>
/// <param name="Registry">展示注册表：条目展示数据经本口读取，含先装载 mod 的条目。</param>
public readonly record struct ModDisplayContext(
    string ModId,
    IDisplayRegistry Registry);
