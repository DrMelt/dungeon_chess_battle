using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Shared.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 单位目录：配置键 ↔ 单位配置，从内容注册表构建。
/// Godot 脚本无 DI 场景经静态单例访问；服务端 DI 场景用构造注入。内容重新装配后应 Rebind。
/// </summary>
/// <remarks>以指定内容注册表构建目录。</remarks>
public sealed class UnitRegistry(ContentSetRegistry registry) : IUnitRegistry {
    /// <summary>全局单例，指向当前内容注册表。</summary>
    public static UnitRegistry Instance { get; private set; } = new(GameContentHost.Registry);

    private readonly ContentSetRegistry _registry = registry;

    /// <summary>内容重装配后重建单例；已注入到 DI 的旧实例引用不变，服务端用构造注入的新实例。</summary>
    public static void Rebind(ContentSetRegistry registry) => Instance = new UnitRegistry(registry);

    /// <summary>全部单位配置。</summary>
    public IReadOnlyCollection<UnitConfig> All => _registry.Units;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public UnitConfig? GetByKey(UnitConfigKey configKey) => _registry.GetUnit(configKey);

    /// <summary>按 UnitConfig 引用反查是否为已注册单位；未注册返回 null。</summary>
    public UnitConfig? GetByConfig(UnitConfig config) =>
        _registry.Units.FirstOrDefault(c => ReferenceEquals(c, config));
}
