using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Battle.Config.Shared.Content;

namespace DungeonChessBattle.Battle.Config.Registry;

/// <summary>
/// 单位目录：配置键 ↔ 单位配置，从内容注册表构建。实例由装配方创建并交给消费方，本类不持全局状态。
/// </summary>
/// <remarks>以指定内容注册表构建目录。</remarks>
public sealed class UnitRegistry(ContentSetRegistry registry) : IUnitRegistry {
    private readonly ContentSetRegistry _registry = registry;

    /// <summary>全部单位配置。</summary>
    public IReadOnlyCollection<UnitConfig> All => _registry.Units;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public UnitConfig? GetByKey(UnitConfigKey configKey) => _registry.GetUnit(configKey);

    /// <summary>按 UnitConfig 引用反查是否为已注册单位；未注册返回 null。</summary>
    public UnitConfig? GetByConfig(UnitConfig config) =>
        _registry.Units.FirstOrDefault(c => ReferenceEquals(c, config));
}
