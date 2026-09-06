using DungeonChessBattle.Battle.Shared.Buffs;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 由 mod 数据运行时构造的 Buff 展示资源：Config 指向 mod 定义的领域 Buff，展示字段经 ApplyViewData 填充。
/// </summary>
/// <remarks>以 mod 定义的 Buff 构建资源；config 为 null 时资源仅承载展示数据不参与领域装配。</remarks>
public sealed partial class ModBuffResource : BuffBaseGodot {
    private readonly BuffDefinition? _config;

    /// <remarks>无内置模板可继承，显示名先回退到 BuffTypeId，mod 声明后由 ApplyViewData 覆盖。</remarks>
    public ModBuffResource(BuffDefinition? config) {
        _config = config;
        if (config is not null)
            ApplyViewData(null, $"Buff {config.BuffTypeId}", null);
    }

    /// <inheritdoc />
    protected override BuffDefinition? Config => _config;
}
