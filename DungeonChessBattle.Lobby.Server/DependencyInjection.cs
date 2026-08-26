using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Server.DataStore.Shared;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Lobby.Server;

/// <summary>
/// Server.Lobby 的 DI 装配扩展。注册大厅配置切片、广播端口与业务协调器。
/// IGameStateStore 与 IBattleRoomManager 由装配层或其它模块扩展注册。
/// </summary>
public static class LobbyServiceCollectionExtensions {
    /// <summary>注册大厅服务器：配置切片、SignalR 广播实现与协调器。</summary>
    public static IServiceCollection AddLobbyServer(this IServiceCollection services, LobbyServerConfig lobbyConfig) {
        services.AddSingleton(lobbyConfig);
        services.AddSingleton<ILobbyBroadcaster>(sp =>
            new SignalRBroadcaster(sp.GetRequiredService<IHubContext<LobbyHub>>()));
        services.AddSingleton<IReplayDownloadTicketStore, ReplayDownloadTicketStore>();
        services.AddSingleton(sp => new GameServer(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ILobbyBroadcaster>(),
            sp.GetRequiredService<LobbyServerConfig>(),
            sp.GetRequiredService<IBattleRoomManager>(),
            sp.GetRequiredService<IGameStateStore>(),
            sp.GetRequiredService<IReplayStore>(),
            sp.GetRequiredService<IReplayDownloadTicketStore>(),
            sp.GetRequiredService<IUnitRegistry>(),
            sp.GetRequiredService<IDungeonRegistry>()));
        services.AddSingleton<ILobbyApplication>(sp => sp.GetRequiredService<GameServer>());
        return services;
    }
}
