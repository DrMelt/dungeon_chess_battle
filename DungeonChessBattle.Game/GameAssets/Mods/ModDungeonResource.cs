namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = DungeonChessBattle.Battle.GameConfig.Models.DungeonConfig;

/// <summary>
/// 由 mod 数据运行时构造的副本展示资源：Config 指向 mod 定义的领域副本，展示字段经 ApplyViewData 填充。
/// </summary>
/// <remarks>以 mod 定义的副本构建资源；config 为 null 时资源仅承载展示数据不参与领域装配。</remarks>
public sealed partial class ModDungeonResource(DungeonConfigDef? config) : DungeonResourceBaseGodot {
    private readonly DungeonConfigDef? _config = config;

    /// <inheritdoc />
    protected override DungeonConfigDef? Config => _config;
}
