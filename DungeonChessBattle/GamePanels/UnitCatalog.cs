using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 客户端单位目录：configKey → 显示名、配置与运行时状态构造的唯一权威注册表。
/// 协议按显示名传输，UI 侧经 GetByDisplayName 反查配置并构造展示用 UnitState 资源。
/// 新增可用单位时只需在此登记；StateFactory 负责运行时 UnitState 实例的构造
/// （GameAssets 的 unit_show.tscn 不携带单位资源，由 MainScene.SpawnUnit 经此注入）。
/// </summary>
public static class UnitCatalog {
    /// <summary>单位条目：配置键、显示名、单位配置与运行时状态工厂。</summary>
    /// <param name="ConfigKey">单位配置键。</param>
    /// <param name="DisplayName">单位显示名。</param>
    /// <param name="Config">单位配置。</param>
    /// <param name="StateFactory">运行时 UnitState 实例工厂。</param>
    public record UnitEntry(string ConfigKey, string DisplayName, UnitConfig Config, System.Func<UnitState> StateFactory);

    /// <summary>按配置键的单位注册表。</summary>
    private static readonly Dictionary<string, UnitEntry> ByKey = new() {
        ["WhiteMage"] = new("WhiteMage", "White Mage", GameConfigDB.UnitWhiteMage, () => new UnitWhiteMage()),
    };

    /// <summary>按显示名反查（供协议显示名 → 配置）。</summary>
    private static readonly Dictionary<string, UnitEntry> ByDisplayName =
        ByKey.Values.ToDictionary(e => e.DisplayName, e => e);

    /// <summary>全部单位条目。</summary>
    public static IEnumerable<UnitEntry> All => ByKey.Values;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public static UnitEntry? GetByKey(string configKey) =>
        ByKey.GetValueOrDefault(configKey);

    /// <summary>按显示名获取单位；不存在返回 null。</summary>
    public static UnitEntry? GetByDisplayName(string displayName) =>
        ByDisplayName.GetValueOrDefault(displayName);
}
