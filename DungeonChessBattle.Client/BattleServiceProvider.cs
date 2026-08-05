using DungeonChessBattle.Logic.Services;

namespace DungeonChessBattle.Client;

/// <summary>
/// 战斗服务提供者，持有当前活跃的服务实例。
/// 支持本地模式（GameLogicService，同时提供完整服务）和网络模式（NetworkBattleClient，仅客户端接口）。
/// </summary>
public class BattleServiceProvider {
    /// <summary>
    /// 客户端接口，本地和网络模式均可用。
    /// </summary>
    public IClientBattleService ClientService {
        get;
    }

    /// <summary>
    /// 服务端接口，仅在本地模式下可用。
    /// </summary>
    public IServerBattleService? ServerService {
        get;
    }

    /// <summary>
    /// 当前模式。
    /// </summary>
    public BattleMode Mode {
        get;
    }

    private BattleServiceProvider(
        IClientBattleService client,
        IServerBattleService? server,
        BattleMode mode) {
        ClientService = client
            ?? throw new ArgumentNullException(nameof(client));
        ServerService = server;
        Mode = mode;
    }

    /// <summary>
    /// 创建本地模式（单人离线）。GameLogicService 同时实现两个接口。
    /// </summary>
    public static BattleServiceProvider CreateLocal(GameLogicService service) {
        return new BattleServiceProvider(service, service, BattleMode.Local);
    }

    /// <summary>
    /// 创建网络模式（多人联网客户端）。仅设置客户端接口。
    /// </summary>
    public static BattleServiceProvider CreateNetwork(IClientBattleService networkClient) {
        return new BattleServiceProvider(networkClient, null, BattleMode.Network);
    }
}

/// <summary>
/// 战斗模式枚举。
/// </summary>
public enum BattleMode {
    /// <summary>未初始化。</summary>
    Uninitialized,

    /// <summary>本地模式（单人离线）。</summary>
    Local,

    /// <summary>网络模式（多人联网）。</summary>
    Network,
}
