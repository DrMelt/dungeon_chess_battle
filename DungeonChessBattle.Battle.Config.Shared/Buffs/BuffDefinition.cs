using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Config.Shared.Buffs;

/// <summary>
/// Buff 只读定义：引擎消费的身份、节拍与叠加规则，加注内容侧行为引用。不可继承，无运行时状态。
/// 引擎侧消费一律经 <see cref="IBuffSpec"/> 取形状；本类是配置层的具体载体。
/// 各 Buff 的数值配置由 <see cref="IBuffEffect"/> 实现自持，效果策略经 <see cref="Effect"/> 引用注入。
/// </summary>
public sealed class BuffDefinition : IBuffSpec {
    /// <summary>Buff 全局唯一键，跨端同步身份。</summary>
    public required BuffTypeId BuffTypeId {
        get; init;
    }

    /// <summary>持续时间，秒。</summary>
    public required double Duration {
        get; init;
    }

    /// <summary>最大叠加层数。</summary>
    public required int MaxStacks {
        get; init;
    }

    /// <summary>伤害类型，实例构造时抄入实例，供效果判定伤害类型；非伤害 Buff 为 None。</summary>
    public DamageType DamageType {
        get; init;
    } = DamageType.None;

    /// <summary>运行时效果策略，由内容层构造注入并自持数值配置。</summary>
    public required IBuffEffect Effect {
        get; init;
    }
}
