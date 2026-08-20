using System.Numerics;
using DungeonChessBattle.Battle.Domain.Buffs;
using DungeonChessBattle.Battle.Domain.Combat.Hates;

namespace DungeonChessBattle.Battle.Domain.Combat;

/// <summary>
/// 单位服务端权威战斗状态，由载体 UnitPawn 持有，BattleScene 经 <see cref="IBattleUnit.RuntimeState"/> 读写推进。
/// 读条目标、Buff、冷却、仇恨权威在此；网络同步经既有投影通道，本状态不参与同步。
/// </summary>
public sealed class UnitCombatState {
    /// <summary>读条目标单位，服务端私有，不参与网络同步；null 表示无。</summary>
    public IBattleUnit? CastTarget {
        get; set;
    }

    /// <summary>读条位置锚点，服务端私有，范围技能使用；null 表示无。</summary>
    public Vector2? CastTargetPos {
        get; set;
    }

    /// <summary>当前生效 Buff 权威列表，服务端推进；结构变化经投影通道同步。</summary>
    public List<ActiveBuff> Buffs { get; } = [];

    /// <summary>个体冷却权威列表，服务端推进；起始与到期经投影通道同步。</summary>
    public List<CooldownEntry> Cooldowns { get; } = [];

    /// <summary>服务端权威仇恨表，本单位对各目标的仇恨；dirty 经投影节流同步，客户端空表不推进。</summary>
    public HateTable Hates { get; } = new();

    /// <summary>清空读条目标。</summary>
    public void ClearCast() {
        CastTarget = null;
        CastTargetPos = null;
    }
}
