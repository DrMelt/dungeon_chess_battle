using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>
/// 把 Buff 只读定义转换为运行时实例，规则收拢于引擎。
/// </summary>
public static class BuffService {
    /// <summary>创建运行时 Buff 实例并绑定来源快照。效果策略由定义 <see cref="BuffDefinition.Effect"/> 提供。</summary>
    public static BuffInstance CreateInstance(BuffDefinition def, UnitId targetNetId, UnitSnapshot? from, UnitId fromNetId) => new() {
        BuffTypeId = def.BuffTypeId,
        TargetNetId = targetNetId,
        FromNetId = fromNetId,
        From = from,
        Remaining = def.Duration,
        MaxStacks = def.MaxStacks,
        Stacks = 1,
        DamageType = def.DamageType,
    };
}
