using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 客户端单位目录：configKey → 显示名与配置的唯一权威注册表。
/// 协议按显示名传输，UI 侧经 GetByDisplayName 反查配置。
/// 新增可用单位时只需在此登记。
/// </summary>
public static class UnitCatalog {
    /// <summary>单位条目：配置键、显示名与单位配置。</summary>
    /// <param name="ConfigKey">单位配置键。</param>
    /// <param name="DisplayName">单位显示名。</param>
    /// <param name="Config">单位配置。</param>
    public record UnitEntry(string ConfigKey, string DisplayName, UnitConfig Config);

    /// <summary>按配置键的单位注册表。</summary>
    private static readonly Dictionary<string, UnitEntry> ByKey = new() {
        ["WhiteMage"] = new("WhiteMage", "White Mage", GameConfigDB.UnitWhiteMage),
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
