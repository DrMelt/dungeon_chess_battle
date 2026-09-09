namespace DungeonChessBattle.Game.GameAssets;

using DungeonConfigDef = Battle.Shared.Content.DungeonConfig;

/// <summary>
/// 由 mod 数据运行时构造的副本展示资源：Config 指向 mod 定义的领域副本，展示字段经 ApplyViewData 填充。
/// </summary>
/// <remarks>以 mod 定义的副本构建资源；config 为 null 时资源仅承载展示数据不参与领域装配。</remarks>
public sealed partial class ModDungeonResource : DungeonResourceBaseGodot {
    private readonly DungeonConfigDef? _config;

    /// <summary>Godot 由脚本类创建资源实例与 <c>Duplicate</c> 都需要无参构造；Config 为 null 时资源仅承载展示数据。</summary>
    public ModDungeonResource() {
    }

    /// <remarks>无模板可继承，显示名先回退到副本键，mod 声明后由 ApplyViewData 覆盖。</remarks>
    public ModDungeonResource(DungeonConfigDef? config) {
        _config = config;
        if (config is not null)
            ApplyViewData(null, config.DungeonKey, null);
    }

    /// <inheritdoc />
    protected override DungeonConfigDef? Config => _config;
}
