using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 客户端单位目录：以 GameConfig.UnitRegistry 为数据源（配置键、显示名、配置）。
/// 客户端与服务端共享同一份配置；技能展示资源由 BattleUnitManager 从 Config.Skills 构建。
/// </summary>
public static class UnitCatalog {
    /// <summary>单位条目：配置键、显示名与单位配置。</summary>
    /// <param name="ConfigKey">单位配置键。</param>
    /// <param name="DisplayName">单位显示名。</param>
    /// <param name="Config">单位配置。</param>
    public record UnitEntry(string ConfigKey, string DisplayName, UnitConfig Config);

    /// <summary>按配置键的单位注册表（数据源：UnitRegistry）。</summary>
    private static readonly Dictionary<string, UnitEntry> ByKey = BuildByKey();

    /// <summary>按显示名反查（供协议显示名 → 配置）。</summary>
    private static readonly Dictionary<string, UnitEntry> ByDisplayName =
        ByKey.Values.ToDictionary(e => e.DisplayName, e => e);

    /// <summary>
    /// 从 UnitRegistry 构建客户端条目：配置键/显示名/配置共享服务端来源。
    /// </summary>
    private static Dictionary<string, UnitEntry> BuildByKey() {
        var dict = new Dictionary<string, UnitEntry>();
        foreach (var entry in UnitRegistry.Instance.All) {
            dict[entry.ConfigKey] = new UnitEntry(
                entry.ConfigKey,
                entry.DisplayName,
                entry.Config);
        }
        return dict;
    }

    /// <summary>全部单位条目。</summary>
    public static IEnumerable<UnitEntry> All => ByKey.Values;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public static UnitEntry? GetByKey(string configKey) =>
        ByKey.GetValueOrDefault(configKey);

    /// <summary>按显示名获取单位；不存在返回 null。</summary>
    public static UnitEntry? GetByDisplayName(string displayName) =>
        ByDisplayName.GetValueOrDefault(displayName);
}
