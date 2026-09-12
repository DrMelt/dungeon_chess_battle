namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 技能效果接口：从只读上下文产出领域事件与待挂载 Buff，无副作用。
/// 由内容包实现并自持数值配置，引擎只按定义引用调用。
/// </summary>
public interface ISkillEffect {
    /// <summary>执行一次技能效果，返回领域事件与待施加 Buff。</summary>
    SkillResolution Resolve(SkillResolveContext ctx);
}
