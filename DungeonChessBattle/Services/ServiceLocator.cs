using DungeonChessBattle.Client;
using DungeonChessBattle.Server;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Services;

/// <summary>
/// 服务定位器，持有 GameServerService 和 GameClientService 的单例。
/// 创建 ILoggerFactory（Console + Godot Provider），注入 Logger 到各 Service。
/// </summary>
public static class ServiceLocator {
    public const int DefaultPort = 10170;

    private static readonly ILoggerFactory LoggerFactoryInstance = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => {
        builder.AddProvider(new GodotLoggerProvider());
    });

    /// <summary>
    /// 获取指定类型的 ILogger 实例。供 Godot 端面板/实体使用，便于排查问题。
    /// </summary>
    public static ILogger<T> GetLogger<T>() => LoggerFactoryInstance.CreateLogger<T>();

    /// <summary>
    /// 通过字符串类别名创建 ILogger。供基类等无法确定具体类型时使用。
    /// </summary>
    public static ILogger CreateLogger(string categoryName) => LoggerFactoryInstance.CreateLogger(categoryName);

    public static readonly GameServerService ServerService = new(
        LoggerFactoryInstance.CreateLogger<GameServerService>(),
        LoggerFactoryInstance);

    public static readonly GameClientService ClientService = new(
        LoggerFactoryInstance);
}
