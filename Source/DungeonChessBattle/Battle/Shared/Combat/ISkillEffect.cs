using ErrorOr;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 技能效果接口：从只读上下文产出领域事件与待挂载 Buff，无副作用。
/// 由内容包实现并自持数值配置，引擎只按定义引用调用。
/// 数值非法等可预期失败以错误返回，由战斗世界记一条日志并跳过本次结算。
/// </summary>
public interface ISkillEffect {
    /// <summary>执行一次技能效果，返回领域事件与待施加 Buff。</summary>
    ErrorOr<SkillResolution> Resolve(SkillResolveContext ctx);
}
