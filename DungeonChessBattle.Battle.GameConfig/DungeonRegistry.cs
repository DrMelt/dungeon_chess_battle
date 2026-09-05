using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Movement;
using DungeonChessBattle.Battle.GameConfig.Models;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 副本目录：副本键 ↔ 副本配置，从内容注册表构建。
/// Godot 脚本无 DI 场景经静态单例访问；服务端 DI 场景用构造注入。内容重新装配后应 Rebind。
/// </summary>
/// <remarks>以指定内容注册表构建目录。</remarks>
public sealed class DungeonRegistry(ContentSetRegistry registry) : IDungeonRegistry {
    /// <summary>全局单例，指向当前内容注册表。</summary>
    public static DungeonRegistry Instance { get; private set; } = new(GameContentHost.Registry);

    private readonly ContentSetRegistry _registry = registry;

    /// <summary>内容重装配后重建单例；已注入到 DI 的旧实例引用不变，服务端用构造注入的新实例。</summary>
    public static void Rebind(ContentSetRegistry registry) => Instance = new DungeonRegistry(registry);

    /// <summary>默认副本键，内容侧定义经登记点暴露。</summary>
    public string DefaultDungeonKey => _registry.Content.DefaultDungeonKey ?? BuiltInContent.DefaultDungeonKey;

    /// <summary>全部副本配置。</summary>
    public IReadOnlyCollection<DungeonConfig> All => _registry.Dungeons;

    /// <summary>按副本键获取配置；不存在或为空返回 null。</summary>
    public DungeonConfig? GetByKey(string? dungeonKey) => _registry.GetDungeon(dungeonKey);

    /// <summary>按副本键获取移动场景布局；副本未配置或不存在时返回默认竞技场。</summary>
    public BattlefieldLayout GetMovementLayout(string? dungeonKey) =>
        GetByKey(dungeonKey)?.Layout ?? BattlefieldLayout.Default;

    /// <summary>按权威副本键获取阵营关系函数；未知键抛异常，杜绝静默回退默认副本。</summary>
    public CampRelationResolver GetRelations(string dungeonKey) =>
        GetByKey(dungeonKey)?.RelationsResolver
        ?? throw new InvalidOperationException($"Unknown dungeon key: {dungeonKey}");
}
