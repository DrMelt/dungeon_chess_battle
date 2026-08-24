namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 技能效果契约：从只读上下文产出领域事件与待挂载 Buff，无副作用。
/// 由内容层 GameConfig 实现，定义承载具体规则。
/// </summary>
public interface ISkillEffect {
    /// <summary>执行一次技能效果，返回领域事件与待施加 Buff。</summary>
    SkillResolution Resolve(SkillResolveContext ctx);
}
