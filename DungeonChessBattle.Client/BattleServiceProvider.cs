using DungeonChessBattle.Logic.Services;

namespace DungeonChessBattle.Client;

/// <summary>
/// 全局战斗服务提供者，持有当前活跃的服务实例。
/// 支持本地模式（GameLogicService，同时提供完整服务）和网络模式（NetworkBattleClient，仅客户端接口）切换。
/// </summary>
public static class BattleServiceProvider {
    private static IClientBattleService? _clientService;
    private static BattleMode _mode = BattleMode.Uninitialized;

    /// <summary>
    /// 当前模式。
    /// </summary>
    public static BattleMode Mode => _mode;

    /// <summary>
    /// 客户端接口，本地和网络模式均可用。
    /// </summary>
    public static IClientBattleService ClientService {
        get => _clientService ?? throw new InvalidOperationException(
            "BattleServiceProvider not initialized. Call InitializeLocal() or InitializeNetwork() first.");
    }

    /// <summary>
    /// 初始化本地模式（单人离线）。同时设置 ClientService。
    /// </summary>
    public static GameLogicService InitializeLocal() {
        var service = new GameLogicService();
        _clientService = service;
        _mode = BattleMode.Local;
        return service;
    }

    /// <summary>
    /// 初始化网络模式（多人联网客户端）。仅设置 ClientService。
    /// </summary>
    public static void InitializeNetwork(IClientBattleService networkClient) {
        _clientService = networkClient;
        _mode = BattleMode.Network;
    }

    /// <summary>
    /// 检查是否已初始化。
    /// </summary>
    public static bool IsInitialized => _clientService != null;

    /// <summary>
    /// 获取本地模式下的 IServerBattleService（仅在本地模式下可用）。
    /// </summary>
    public static IServerBattleService? TryGetServerService() =>
        _clientService as IServerBattleService;
}

/// <summary>
/// 战斗模式枚举。
/// </summary>
public enum BattleMode {
    Uninitialized,
    Local,
    Network,
}
