using System.Collections.Generic;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 客户端单位目录：以 GameConfig.UnitRegistry 为数据源，按配置键索引单位配置。
/// 客户端与服务端共享同一份配置；技能展示资源由 BattleUnitManager 从 Config.Skills 构建。
/// </summary>
public static class UnitCatalog {
    /// <summary>按配置键的单位注册表（数据源：UnitRegistry）。</summary>
    private static readonly Dictionary<string, UnitConfig> ByKey = BuildByKey();

    /// <summary>
    /// 从 UnitRegistry 构建客户端目录：单位配置共享服务端来源。
    /// </summary>
    private static Dictionary<string, UnitConfig> BuildByKey() {
        var dict = new Dictionary<string, UnitConfig>();
        foreach (var config in UnitRegistry.Instance.All)
            dict[config.ConfigKey] = config;
        return dict;
    }

    /// <summary>全部单位配置。</summary>
    public static IEnumerable<UnitConfig> All => ByKey.Values;

    /// <summary>按配置键获取单位配置；不存在返回 null。</summary>
    public static UnitConfig? GetByKey(string configKey) =>
        ByKey.GetValueOrDefault(configKey);
}
