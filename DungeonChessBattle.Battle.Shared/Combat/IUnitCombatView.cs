namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 单位战斗核心公共面：身份、数值与技能来源的聚合。
/// 被 <see cref="ISkillCasterView"/> 与 <see cref="IUnitUiView"/> 共享，避免各自重复声明同一组基础接口。
/// </summary>
public interface IUnitCombatView : IUnitIdentityView, ICombatValuesView, ISkillSource {
}
