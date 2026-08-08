using System.Collections.Generic;
using System.Linq;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GamePanels;

/// <summary>
/// 客户端单位目录：以 GameConfig.UnitRegistry 为数据源（配置键、显示名、配置），
/// 仅补充 Godot 侧专属的运行时状态工厂（StateFactory）。
/// 新增单位只需在 UnitRegistry 登记一次，客户端与服务端共享同一份配置；
/// StateFactory 负责运行时 UnitState 实例的构造（GameAssets 的 unit_show.tscn
/// 不携带单位资源，由 MainScene.SpawnUnit 经此注入）。
/// </summary>
public static class UnitCatalog {
    /// <summary>单位条目：配置键、显示名、单位配置与运行时状态工厂。</summary>
    /// <param name="ConfigKey">单位配置键。</param>
    /// <param name="DisplayName">单位显示名。</param>
    /// <param name="Config">单位配置。</param>
    /// <param name="StateFactory">运行时 UnitState 实例工厂。</param>
    public record UnitEntry(string ConfigKey, string DisplayName, UnitConfig Config, System.Func<UnitState> StateFactory);

    /// <summary>按配置键的单位注册表（数据源：UnitRegistry）。</summary>
    private static readonly Dictionary<string, UnitEntry> ByKey = BuildByKey();

    /// <summary>按显示名反查（供协议显示名 → 配置）。</summary>
    private static readonly Dictionary<string, UnitEntry> ByDisplayName =
        ByKey.Values.ToDictionary(e => e.DisplayName, e => e);

    /// <summary>
    /// 从 UnitRegistry 构建客户端条目：配置键/显示名/配置共享服务端来源，
    /// 仅补 StateFactory（客户端运行时资源工厂，按配置键分发）。
    /// 新增单位时若需要新的 Godot 运行时类型，在此补一条分发即可。
    /// </summary>
    private static Dictionary<string, UnitEntry> BuildByKey() {
        var dict = new Dictionary<string, UnitEntry>();
        foreach (var entry in UnitRegistry.Instance.All) {
            dict[entry.ConfigKey] = new UnitEntry(
                entry.ConfigKey,
                entry.DisplayName,
                entry.Config,
                CreateStateFactory(entry.ConfigKey));
        }
        return dict;
    }

    /// <summary>按配置键返回运行时 UnitState 工厂；未知配置键返回空操作。</summary>
    private static System.Func<UnitState> CreateStateFactory(string configKey) =>
        configKey switch {
            "WhiteMage" => () => new UnitWhiteMage(),
            // 非静态 lambda：需捕获 configKey 用于错误信息
            _ => () => throw new System.InvalidOperationException(
                $"UnitCatalog: no StateFactory registered for config key '{configKey}'. " +
                "Add a factory in BuildByKey/CreateStateFactory."),
        };

    /// <summary>全部单位条目。</summary>
    public static IEnumerable<UnitEntry> All => ByKey.Values;

    /// <summary>按配置键获取单位；不存在返回 null。</summary>
    public static UnitEntry? GetByKey(string configKey) =>
        ByKey.GetValueOrDefault(configKey);

    /// <summary>按显示名获取单位；不存在返回 null。</summary>
    public static UnitEntry? GetByDisplayName(string displayName) =>
        ByDisplayName.GetValueOrDefault(displayName);
}
