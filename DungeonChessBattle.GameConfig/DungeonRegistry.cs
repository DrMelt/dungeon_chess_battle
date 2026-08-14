using DungeonChessBattle.Battle.Domain.Movement;
using DungeonChessBattle.GameConfig.Data;

namespace DungeonChessBattle.GameConfig;

/// <summary>
/// 副本目录的纯 C# 权威注册表：副本键 ↔ 副本配置。
/// 服务端按房间选中的副本键生成敌人，客户端按副本键选择环境表现。
/// </summary>
public sealed class DungeonRegistry {
    /// <summary>副本条目。</summary>
    /// <param name="Config">副本配置。</param>
    public sealed record DungeonEntry(DungeonConfig Config);

    /// <summary>全局单例。</summary>
    public static readonly DungeonRegistry Instance = new();

    private readonly Dictionary<string, DungeonConfig> _byKey;

    /// <summary>默认副本键，配置缺失时退回。</summary>
    public const string DefaultDungeonKey = "dungeon_01";

    private DungeonRegistry() {
        // 新增副本在此登记，唯一注册点
        var entries = new[] {
            GameConfigDB.Dungeon_01,
            GameConfigDB.Dungeon_02,
        };

        // fail-fast：敌人生成引用必须在 UnitRegistry 已注册，配错配置即启动失败，杜绝静默降级
        foreach (var dungeon in entries) {
            foreach (var spawn in dungeon.Enemies) {
                if (UnitRegistry.Instance.GetByConfig(spawn.Unit) == null) {
                    throw new InvalidOperationException(
                        $"Dungeon '{dungeon.DungeonKey}' enemy spawn references unregistered unit config.");
                }
            }
        }

        _byKey = entries.ToDictionary(d => d.DungeonKey, d => d);
    }

    /// <summary>全部副本配置。</summary>
    public IReadOnlyCollection<DungeonConfig> All => _byKey.Values;

    /// <summary>按副本键获取配置；不存在或为空返回 null。</summary>
    public DungeonConfig? GetByKey(string? dungeonKey) =>
        string.IsNullOrWhiteSpace(dungeonKey) ? null : _byKey.GetValueOrDefault(dungeonKey);

    /// <summary>按副本键获取移动场景布局；副本未配置或不存在时返回默认竞技场。</summary>
    public BattlefieldLayout GetMovementLayout(string? dungeonKey) =>
        GetByKey(dungeonKey)?.Layout ?? BattlefieldLayout.Default;

    /// <summary>按副本键获取显示名；配置缺失时返回 null。</summary>
    public string? GetDisplayName(string? dungeonKey) => GetByKey(dungeonKey)?.DisplayName;
}
