namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 技能只读仓库，依赖倒置，由配置层 GameConfig 实现。
/// Logic 层经此接口按技能 ID 获取技能定义进行结算，不依赖具体配置表实现。
/// </summary>
public interface ISkillRepository {
    /// <summary>按技能 ID 获取技能定义；不存在时返回 null。</summary>
    SkillDefinition? Get(ushort skillId);
}
