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

    private static readonly ILoggerFactory LoggerFactory = Microsoft.Extensions.Logging.LoggerFactory.Create(builder => {
        builder.AddProvider(new GodotLoggerProvider());
    });

    public static readonly GameServerService ServerService = new(
        LoggerFactory.CreateLogger<GameServerService>(),
        LoggerFactory);

    public static readonly GameClientService ClientService = new(
        LoggerFactory.CreateLogger<GameClientService>(),
        LoggerFactory);
}
