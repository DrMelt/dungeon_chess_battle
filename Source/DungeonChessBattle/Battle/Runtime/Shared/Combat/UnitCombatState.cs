using System.Numerics;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Runtime.Shared.Buffs;
using DungeonChessBattle.Battle.Runtime.Shared.Combat.Hates;

namespace DungeonChessBattle.Battle.Runtime.Shared.Combat;

/// <summary>
/// 单位权威战斗状态，由 <see cref="BattleUnit.RuntimeState"/> 持有，BattleScene 读写推进。
/// 读条目标、Buff、冷却、仇恨权威在此；Buff 与冷却经 <c>UnitPawn</c> 同步通道搬运，
/// 仇恨只下行不回填。按帧施法意图不属本状态，见 <c>BattleUnit.CastInput</c>。
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

    /// <summary>全局冷却组权威列表，服务端推进；在线端为同步通道回填的展示壳。每个组键至多一条。</summary>
    public List<GcdEntry> Gcds { get; } = [];

    /// <summary>个体冷却权威列表，服务端推进；在线端为同步通道回填的展示壳。</summary>
    public List<CooldownEntry> Cooldowns { get; } = [];

    /// <summary>服务端权威仇恨表，本单位对各目标的仇恨；随投影下行，在线端不消费。</summary>
    public HateTable Hates { get; } = new();

    /// <summary>清空读条目标。</summary>
    public void ClearCast() {
        CastTarget = null;
        CastTargetPos = null;
    }

    /// <summary>捕获状态副本：读条目标按单位标识入副本，列表项逐项复制。</summary>
    public UnitCombatStateCopy CaptureState() => new(
        CastTarget?.UnitId ?? UnitId.None, CastTargetPos,
        [.. Buffs.Select(buff => buff.Copy())],
        [.. Gcds.Select(entry => entry.Copy())],
        [.. Cooldowns.Select(entry => entry.Copy())],
        Hates.Snapshot());

    /// <summary>
    /// 按状态副本重写全部权威状态：读条目标、Buff、冷却与仇恨整体替换，不合并。
    /// 副本项再复制后写入，副本本身不与活动对象共享可变状态；读条目标由调用方按标识解析后传入。
    /// </summary>
    public void RestoreState(UnitCombatStateCopy state, BattleUnit? castTarget) {
        CastTarget = castTarget;
        CastTargetPos = state.CastTargetPos;

        Buffs.Clear();
        foreach (var buff in state.Buffs)
            Buffs.Add(buff.Copy());

        Gcds.Clear();
        foreach (var gcd in state.Gcds)
            Gcds.Add(gcd.Copy());

        Cooldowns.Clear();
        foreach (var cooldown in state.Cooldowns)
            Cooldowns.Add(cooldown.Copy());

        Hates.Restore(state.Hates);
    }
}

/// <summary>
/// 运行时状态副本：读条目标以单位标识表达，不持单位引用；
/// Buff、冷却与仇恨均为独立副本，不与活动对象共享可变状态。
/// </summary>
public sealed record UnitCombatStateCopy(
    UnitId CastTargetId,
    Vector2? CastTargetPos,
    IReadOnlyList<ActiveBuff> Buffs,
    IReadOnlyList<GcdEntry> Gcds,
    IReadOnlyList<CooldownEntry> Cooldowns,
    IReadOnlyList<HateSnapshot> Hates);
