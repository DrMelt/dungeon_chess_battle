using System.Numerics;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat.Hates;

namespace DungeonChessBattle.Battle.Shared.Combat;

/// <summary>
/// 单位权威战斗状态，由 <see cref="BattleUnit.RuntimeState"/> 持有，BattleScene 读写推进。
/// 读条目标、Buff、冷却、仇恨权威在此；Buff 与冷却经 <c>UnitPawn</c> 同步通道搬运，仇恨只下行不回填。
/// </summary>
public sealed class UnitCombatState {
    /// <summary>读条目标单位，服务端私有，不参与网络同步；null 表示无。</summary>
    public BattleUnit? CastTarget {
        get; set;
    }

    /// <summary>读条位置锚点，服务端私有，范围技能使用；null 表示无。</summary>
    public Vector2? CastTargetPos {
        get; set;
    }

    /// <summary>当前生效 Buff 权威列表，服务端推进；在线端为同步通道回填的展示壳。</summary>
    public List<ActiveBuff> Buffs { get; } = [];

    /// <summary>个体冷却权威列表，服务端推进；在线端为同步通道回填的展示壳。</summary>
    public List<CooldownEntry> Cooldowns { get; } = [];

    /// <summary>服务端权威仇恨表，本单位对各目标的仇恨；随投影下行，在线端不消费。</summary>
    public HateTable Hates { get; } = new();

    /// <summary>清空读条目标。</summary>
    public void ClearCast() {
        CastTarget = null;
        CastTargetPos = null;
    }
}
