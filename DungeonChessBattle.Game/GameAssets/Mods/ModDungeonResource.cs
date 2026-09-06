namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = DungeonChessBattle.Battle.Shared.Content.DungeonConfig;

/// <summary>
/// 由 mod 数据运行时构造的副本展示资源：Config 指向 mod 定义的领域副本，展示字段经 ApplyViewData 填充。
/// </summary>
/// <remarks>以 mod 定义的副本构建资源；config 为 null 时资源仅承载展示数据不参与领域装配。</remarks>
public sealed partial class ModDungeonResource : DungeonResourceBaseGodot {
    private readonly DungeonConfigDef? _config;

    /// <remarks>无内置模板可继承，显示名先回退到副本键，mod 声明后由 ApplyViewData 覆盖。</remarks>
    public ModDungeonResource(DungeonConfigDef? config) {
        _config = config;
        if (config is not null)
            ApplyViewData(null, config.DungeonKey, null);
    }

    /// <inheritdoc />
    protected override DungeonConfigDef? Config => _config;
}
