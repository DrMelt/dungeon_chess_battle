using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 由 mod 数据运行时构造的技能展示资源：Config 指向 mod 定义的领域技能，展示字段经 ApplyViewData 填充。
/// 不需 [GlobalClass]，不入编辑器资源表，仅作为运行时 resource 模板被 ResourceTables 注册。
/// </summary>
/// <remarks>以 mod 定义的技能构建资源；config 为 null 时资源仅承载展示数据不参与领域装配。</remarks>
public sealed partial class ModSkillResource(SkillDefinition? config) : UnitSkillBaseGodot {
    private readonly SkillDefinition? _config = config;

    /// <inheritdoc />
    protected override SkillDefinition? Config => _config;
}
