using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 单位目录的纯 C# 权威注册表：配置键 ↔ 单位配置。
/// 服务端建模与控制器绑定校验、客户端 UnitCatalog 展示共享同一份配置，
/// 新增单位只需在此登记一处。协议与实体按配置键 ConfigKey 传输。
/// </summary>
public sealed class UnitRegistry {
    /// <summary>全局单例。</summary>
    public static readonly UnitRegistry Instance = new();

    private readonly Dictionary<string, UnitConfig> _byKey;

    private UnitRegistry() {
        // 新增单位在此登记，唯一注册点；敌人单位一并注册供服务端生成与客户端渲染
        var configs = new[] {
            GameConfigDB.UnitWhiteMage,
            GameConfigDB.UnitGoblin,
            GameConfigDB.UnitGoblinBoss,
        };
        _byKey = configs.ToDictionary(c => c.ConfigKey, c => c);
    }

    /// <summary>全部单位配置。</summary>
    public IReadOnlyCollection<UnitConfig> All => _byKey.Values;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public UnitConfig? GetByKey(string configKey) =>
        _byKey.GetValueOrDefault(configKey);

    /// <summary>按 UnitConfig 引用反查是否为已注册单位；未注册返回 null。</summary>
    public UnitConfig? GetByConfig(UnitConfig config) =>
        _byKey.Values.FirstOrDefault(c => ReferenceEquals(c, config));
}
