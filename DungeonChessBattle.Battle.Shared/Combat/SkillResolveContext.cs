using System.Numerics;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>技能结算只读上下文：纯函数输入，不含可变状态，无副作用。</summary>
/// <param name="Skill">目标技能定义，效果实现按子类型读取数据。</param>
/// <param name="Caster">施法单位只读视图。</param>
/// <param name="Target">单位目标；无单位目标需求时为空。</param>
/// <param name="TargetPos">位置目标；无位置目标需求时为空。</param>
/// <param name="Candidates">候选单位表，范围技能遍历用。</param>
/// <param name="Relations">副本配置的阵营关系函数。</param>
public readonly record struct SkillResolveContext(
    SkillDefinition Skill,
    IBattleUnitView Caster,
    IBattleUnitView? Target,
    Vector2? TargetPos,
    IReadOnlyList<IBattleUnitView> Candidates,
    CampRelationResolver Relations);

/// <summary>技能结算结果：领域事件 + 待挂载的新 Buff 描述。</summary>
public sealed record SkillResolution(
    IReadOnlyList<IBattleEvent> Events,
    IReadOnlyList<BuffToApply> Buffs) {
    /// <summary>无产出结果。</summary>
    public static SkillResolution Empty { get; } = new([], []);
}

/// <summary>待施加的 Buff 描述，由编排层落账为运行时 Buff 实例。</summary>
public readonly record struct BuffToApply(
    BuffDefinition Definition,
    ushort TargetNetId,
    UnitSnapshot? From,
    ushort FromNetId);
