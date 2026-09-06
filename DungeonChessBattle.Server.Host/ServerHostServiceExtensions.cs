using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.GameConfig;
using DungeonChessBattle.Battle.Server;
using DungeonChessBattle.Lobby.Server;
using DungeonChessBattle.Replay.Server;
using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Server.DataStore;
using DungeonChessBattle.Server.DataStore.Shared;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// Server.Host 的 DI 装配扩展：注册存储、内容目录、大厅/战斗/回放模块与 SignalR，
/// 并把 <see cref="ServerConfig"/> 映射为各模块配置切片。
/// </summary>
public static class ServerHostServiceExtensions {
    /// <summary>注册服务器全部服务装配。</summary>
    public static IServiceCollection AddServerHost(this IServiceCollection services,
        ServerConfig config, ILoggerFactory loggerFactory) {
        // 进程内内存存储；引入持久化实现时只需替换此两处注册
        services.AddSingleton<IGameStateStore>(_ => new InMemoryGameStateStore(loggerFactory));
        services.AddSingleton<IReplayStore>(new InMemoryReplayStore());

        services.AddSingleton<IPlayerIdentityResolver>(sp =>
            new PlayerIdentityResolver(sp.GetRequiredService<IGameStateStore>()));
        // 内容装配已在入口完成 Rebind，这里绑定当前实例
        services.AddSingleton<IUnitRegistry>(UnitRegistry.Instance);
        services.AddSingleton<IDungeonRegistry>(DungeonRegistry.Instance);

        services.AddLobbyServer(new LobbyServerConfig { ServerPassword = config.ServerPassword });
        // 有服务器密码时以密码为房间握手指纹，否则用协议默认连接密钥
        services.AddBattleServer(new BattleServerConfig {
            ConnectionKey = config.ServerPassword ?? NetworkDefaults.ConnectionKey,
        });
        services.AddReplayServer();
        services.AddSignalR();
        return services;
    }
}
