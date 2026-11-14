using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Runtime.Shared.Buffs;

namespace DungeonChessBattle.Battle.Logic.Buffs;

/// <summary>
/// 把 Buff 只读定义转换为运行时实例，规则收拢于引擎。
/// </summary>
public static class BuffService {
    /// <summary>创建运行时 Buff 实例并绑定来源快照。效果策略由规格 <see cref="IBuffSpec.Effect"/> 提供。</summary>
    public static BuffInstance CreateInstance(
        IBuffSpec def, UnitId targetUnitId, UnitSnapshot? from, UnitId sourceUnitId) => new() {
            BuffTypeId = def.BuffTypeId,
            TargetUnitId = targetUnitId,
            SourceUnitId = sourceUnitId,
            From = from,
            Remaining = def.Duration,
            Stacks = 1,
            DamageType = def.DamageType,
        };
}
