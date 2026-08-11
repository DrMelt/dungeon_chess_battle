using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 单位目录的纯 C# 权威注册表：配置键 / 显示名 ↔ 单位配置。
/// 服务端建模与控制器绑定校验、客户端 UnitCatalog 展示共享同一份配置，
/// 新增单位只需在此登记一处。协议按显示名 DisplayName 传输。
/// </summary>
public sealed class UnitRegistry {
    /// <summary>单位条目：配置键、显示名与单位配置。</summary>
    /// <param name="ConfigKey">单位配置键，内部标识。</param>
    /// <param name="DisplayName">单位显示名，协议传输用。</param>
    /// <param name="Config">单位配置数据。</param>
    public sealed record UnitEntry(string ConfigKey, string DisplayName, UnitConfig Config);

    /// <summary>全局单例。</summary>
    public static readonly UnitRegistry Instance = new();

    private readonly Dictionary<string, UnitEntry> _byKey;
    private readonly Dictionary<string, UnitEntry> _byDisplayName;

    private UnitRegistry() {
        // 新增单位在此登记，唯一注册点
        var entries = new[] {
            new UnitEntry("WhiteMage", "White Mage", GameConfigDB.UnitWhiteMage),
        };
        _byKey = entries.ToDictionary(e => e.ConfigKey, e => e);
        _byDisplayName = entries.ToDictionary(e => e.DisplayName, e => e);
    }

    /// <summary>全部单位条目。</summary>
    public IReadOnlyCollection<UnitEntry> All => _byKey.Values;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public UnitEntry? GetByKey(string configKey) =>
        _byKey.GetValueOrDefault(configKey);

    /// <summary>按显示名获取单位；不存在返回 null。</summary>
    public UnitEntry? GetByDisplayName(string displayName) =>
        _byDisplayName.GetValueOrDefault(displayName);
}
