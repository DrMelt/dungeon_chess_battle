using DungeonChessBattle.Logic.Services;

namespace DungeonChessBattle.Client;

/// <summary>
/// 全局战斗服务提供者，持有当前活跃的 IBattleService 实例。
/// 支持本地模式（GameLogicService）和网络模式（NetworkBattleClient）切换。
/// </summary>
public static class BattleServiceProvider {
    private static IBattleService? _instance;

    /// <summary>
    /// 当前活跃的战斗服务实例。
    /// 离线模式下使用 GameLogicService，联网模式下使用 NetworkBattleClient。
    /// </summary>
    public static IBattleService Service {
        get => _instance ?? throw new InvalidOperationException("BattleServiceProvider not initialized. Call InitializeLocal() or InitializeNetwork() first.");
        set => _instance = value;
    }

    /// <summary>
    /// 初始化本地模式（单人离线）。
    /// </summary>
    public static void InitializeLocal() {
        _instance = new GameLogicService();
    }

    /// <summary>
    /// 初始化网络模式（多人联网客户端）。
    /// </summary>
    public static void InitializeNetwork(IBattleService networkClient) {
        _instance = networkClient;
    }

    /// <summary>
    /// 检查是否已初始化。
    /// </summary>
    public static bool IsInitialized => _instance != null;
}