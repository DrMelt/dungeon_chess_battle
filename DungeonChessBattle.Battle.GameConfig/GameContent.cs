using DungeonChessBattle.Battle.Shared.Content;

namespace DungeonChessBattle.Battle.GameConfig;

/// <summary>
/// 一次内容装配的产出：内容注册表与建在其上的单位目录。
/// 不持全局状态，由装配方创建后交宿主组合根持有，消费方按需取用其中的视图。
/// 副本不经目录口：按内容键查副本走 <see cref="IContentRegistryView.GetDungeon"/>，枚举走注册表的副本集合。
/// </summary>
/// <param name="registry">本次装配的内容注册表，领域定义与内容修订号所在。</param>
public sealed class GameContent(ContentSetRegistry registry) {
    /// <summary>内容注册表：全部领域定义、副本集合与内容修订号。</summary>
    public ContentSetRegistry Registry {
        get;
    } = registry;

    /// <summary>单位目录：配置键 ↔ 单位配置，另含按定义反查。</summary>
    public IUnitRegistry Units {
        get;
    } = new UnitRegistry(registry);
}
