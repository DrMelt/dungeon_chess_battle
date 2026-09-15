using System.Collections.Generic;
using DungeonChessBattle.Battle.Shared.ValueObjects;
using DungeonChessBattle.Game.Services;
using DungeonChessBattle.Battle.Config.Shared.Content;

namespace DungeonChessBattle.Game.GamePanels;

/// <summary>
/// 客户端单位目录：以装配产物的单位目录为数据源，按配置键索引单位配置。
/// 客户端与服务端共享同一份配置；单位携带的技能定义引用即施法规则来源，展示数据按键取自展示取数入口。
/// </summary>
public static class UnitCatalog {
    /// <summary>按配置键的单位注册表（数据源：装配产物的单位目录）。</summary>
    private static readonly Dictionary<UnitConfigKey, UnitConfig> ByKey = BuildByKey();

    /// <summary>从装配产物的单位目录构建客户端目录：单位配置共享服务端来源。</summary>
    private static Dictionary<UnitConfigKey, UnitConfig> BuildByKey() {
        var dict = new Dictionary<UnitConfigKey, UnitConfig>();
        foreach (var config in ServiceLocator.GameContent.Units.All)
            dict[config.ConfigKey] = config;
        return dict;
    }

    /// <summary>全部单位配置。</summary>
    public static IEnumerable<UnitConfig> All => ByKey.Values;

    /// <summary>按配置键获取单位配置；不存在返回 null。</summary>
    public static UnitConfig? GetByKey(UnitConfigKey configKey) =>
        ByKey.GetValueOrDefault(configKey);
}
