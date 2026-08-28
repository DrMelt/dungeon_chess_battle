using Microsoft.Extensions.DependencyInjection;

namespace DungeonChessBattle.Replay.Server;

/// <summary>
/// Server.Replay 的 DI 装配扩展。注册回放查询与归档读取业务。
/// IReplayStore 与 IPlayerIdentityResolver 由存储层与装配层提供。
/// </summary>
public static class ReplayServiceCollectionExtensions {
    /// <summary>注册回放服务端业务。</summary>
    public static IServiceCollection AddReplayServer(this IServiceCollection services) {
        services.AddSingleton<ReplayServer>();
        return services;
    }
}
