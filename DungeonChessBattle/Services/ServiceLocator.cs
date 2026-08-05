using DungeonChessBattle.Client;
using DungeonChessBattle.Server;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Services;

/// <summary>
/// 服务定位器，持有 GameServerHost 和 GameClientService 的单例。
/// 创建 ILoggerFactory（Console + Godot Provider），注入 Logger 到各 Service。
/// </summary>
public static class ServiceLocator {
    /// <summary>默认服务器端口。</summary>
    public const int DefaultPort = 10170;

    /// <summary>日志工厂实例（Godot 控制台 Provider）。</summary>
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

    /// <summary>内嵌游戏服务器单例。</summary>
    public static readonly GameServerHost ServerService = new(
        LoggerFactoryInstance.CreateLogger<GameServerHost>(),
        LoggerFactoryInstance);

    /// <summary>游戏客户端服务单例。</summary>
    public static readonly GameClientService ClientService = new(
        LoggerFactoryInstance);
}
