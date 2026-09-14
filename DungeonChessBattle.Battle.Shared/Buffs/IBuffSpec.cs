using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.ValueObjects;

namespace DungeonChessBattle.Battle.Shared.Buffs;

/// <summary>
/// Buff 规格只读接口：引擎消费的 Buff 形状，由内容定义实现。
/// 只声明身份、时长、叠加规则与效果引用。
/// </summary>
public interface IBuffSpec {
    /// <summary>Buff 全局唯一键，跨端同步身份。</summary>
    BuffTypeId BuffTypeId {
        get;
    }

    /// <summary>持续时间，秒。</summary>
    double Duration {
        get;
    }

    /// <summary>最大叠加层数。</summary>
    int MaxStacks {
        get;
    }

    /// <summary>伤害类型；非伤害 Buff 为 None。</summary>
    DamageType DamageType {
        get;
    }

    /// <summary>运行时效果策略。</summary>
    IBuffEffect Effect {
        get;
    }
}
