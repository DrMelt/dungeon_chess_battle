using DungeonChessBattle.Server.Abstractions;
using DungeonChessBattle.Server.Battle.Replay;
using DungeonChessBattle.Server.StateStore.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Battle;

/// <summary>
/// Server.Battle 的 DI 装配扩展。注册战斗配置切片与房间生命周期管理器实现，
/// IBattleRoomManager 契约供大厅协调层经 DI 消费。
/// </summary>
public static class BattleServiceCollectionExtensions {
    /// <summary>注册战斗服务器：配置切片与 IBattleRoomManager 绑定。</summary>
    public static IServiceCollection AddBattleServer(this IServiceCollection services, BattleServerConfig battleConfig) {
        services.AddSingleton(battleConfig);
        services.AddSingleton<IReplayStore>(new InMemoryReplayStore());
        services.AddSingleton<IBattleRoomManager>(sp => new BattleRoomManager(
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<IGameStateStore>(),
            sp.GetRequiredService<BattleServerConfig>(),
            sp.GetRequiredService<IReplayStore>()));
        return services;
    }
}
