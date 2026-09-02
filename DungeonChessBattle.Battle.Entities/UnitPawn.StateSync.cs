using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Entities.SyncData;
using LiteEntitySystem.Extensions;

namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// UnitPawn 与领域 BattleUnit 的状态同步通道：字段清单唯一声明处，两方向逐字段成对。
/// 只承载运行时动态状态；最大生命值等基础数值两端经同一份配置只读视图获取，不进本通道。
/// <see cref="SyncFrom"/> 是服务端权威投影（领域 → 载体），<see cref="SyncInto"/> 是在线端回填（载体 → 领域），
/// 不设端别守卫，调用点负责选向；误端调用即覆写对端状态而非空操作。
/// 倒计时字段线上语义固定为截止 tick、领域侧固定为剩余秒，换算在本通道内双向闭合：
/// 截止 tick 在条目存续期内恒定，剩余秒是每帧派生值，故列表指纹只跳过重建，不跳过剩余秒刷新。
/// 新增同步字段必须同时补齐两个方向，只出现一侧即漏配。
/// </summary>
public partial class UnitPawn {
    /// <summary>回填指纹归属的领域单位；换绑（含 LES 实体池复用）即指纹失效，不依赖调用方通知。</summary>
    private BattleUnit? _stampOwner;

    /// <summary>上次回填的 Buff 列表内容指纹，未变化时跳过列表重建；<see cref="int.MinValue"/> 表示未回填过。</summary>
    private int _buffStamp = int.MinValue;

    /// <summary>上次回填的冷却列表内容指纹，未变化时跳过列表重建。</summary>
    private int _cooldownStamp = int.MinValue;

    /// <summary>上次回填的全局冷却组列表内容指纹，未变化时跳过列表重建。</summary>
    private int _gcdStamp = int.MinValue;

    /// <summary>
    /// 服务端投影：把领域单位权威状态写入本实体 SyncVar。标量由 LES 增量 diff 省流，
    /// 冷却/Buff/仇恨列表内容比对后节流重建。
    /// </summary>
    public void SyncFrom(BattleUnit unit) {
        Position.Value = unit.Position;
        Direction.Value = unit.Direction;
        Health.Value = unit.Health;
        SkillCasting.Value = unit.SkillCasting.Id;
        SkillCastRemaining.Value = unit.SkillCastRemaining;
        FocusTargetNetId.Value = unit.FocusTarget;
        ProjectGcds(unit);
        ProjectCooldowns(unit);
        ProjectBuffs(unit);
        ProjectHates(unit);
    }

    /// <summary>
    /// 在线端回填：把本实体 SyncVar 读数覆写进本地领域单位，作为展示与判定的取数源。
    /// 本实体持有的冷却/Buff 领域列表由本方法独占维护，本地改写会被指纹跳过掩盖。
    /// HatesList 只下行不回填：在线端不跑仇恨结算与 AI；聚焦随本通道双向回填，供 UI 直读。
    /// </summary>
    public void SyncInto(BattleUnit unit) {
        // 换绑即失效：LES 实体池复用的载体带着上一个单位的残留指纹
        if (!ReferenceEquals(unit, _stampOwner)) {
            _stampOwner = unit;
            _buffStamp = _cooldownStamp = _gcdStamp = int.MinValue;
        }

        unit.Position = Position.Value;
        unit.Direction = Direction.Value;
        unit.Health = Health.Value;
        unit.SkillCasting = string.IsNullOrEmpty(SkillCasting.Value) ? default : new SkillKeyId(SkillCasting.Value);
        unit.SkillCastRemaining = SkillCastRemaining.Value;
        unit.FocusTarget = FocusTargetNetId.Value;
        ApplyGcds(unit);
        ApplyCooldowns(unit);
        ApplyBuffs(unit);
    }

    /// <summary>个体冷却整包投影，内容一致时跳过，避免每帧重建产生网络流量。</summary>
    private void ProjectCooldowns(BattleUnit unit) {
        var cds = unit.Cooldowns;
        var entries = new SyncSkillCooldownSnapshot.Entry[cds.Count];
        for (int i = 0; i < cds.Count; i++)
            entries[i] = new SyncSkillCooldownSnapshot.Entry(
                cds[i].SkillKey.Id,
                SyncTickHelper.EndTick(EntityManager, cds[i].Remaining));

        var current = SkillCooldowns.Value;
        bool changed;
        if (current == null) {
            changed = true;
        }
        else {
            changed = current.Entries.Count != entries.Length;
            if (!changed) {
                for (int i = 0; i < entries.Length; i++) {
                    if (current.Entries[i].SkillId != entries[i].SkillId
                        || current.Entries[i].EndServerTick != entries[i].EndServerTick) {
                        changed = true;
                        break;
                    }
                }
            }
        }
        if (!changed)
            return;

        var snapshot = new SyncSkillCooldownSnapshot();
        snapshot.Set(entries);
        SkillCooldowns.Value = snapshot;
    }

    /// <summary>全局冷却组整包投影，内容一致时跳过，避免每帧重建产生网络流量。</summary>
    private void ProjectGcds(BattleUnit unit) {
        var gcds = unit.RuntimeState.Gcds;
        var entries = new SyncGcdSnapshot.Entry[gcds.Count];
        for (int i = 0; i < gcds.Count; i++)
            entries[i] = new SyncGcdSnapshot.Entry(
                gcds[i].GroupKey,
                SyncTickHelper.EndTick(EntityManager, gcds[i].Remaining));

        var current = Gcds.Value;
        bool changed;
        if (current == null) {
            changed = true;
        }
        else {
            changed = current.Entries.Count != entries.Length;
            if (!changed) {
                for (int i = 0; i < entries.Length; i++) {
                    if (current.Entries[i].GroupKey != entries[i].GroupKey
                        || current.Entries[i].EndServerTick != entries[i].EndServerTick) {
                        changed = true;
                        break;
                    }
                }
            }
        }
        if (!changed)
            return;

        var snapshot = new SyncGcdSnapshot();
        snapshot.Set(entries);
        Gcds.Value = snapshot;
    }

    /// <summary>Buff 全量投影，内容一致时跳过；剩余秒数落为截止 tick。</summary>
    private void ProjectBuffs(BattleUnit unit) {
        var buffs = unit.Buffs;
        bool changed = BuffsList.Count != buffs.Count;
        if (!changed) {
            for (int i = 0; i < buffs.Count; i++) {
                var existing = BuffsList[i];
                var b = buffs[i].Instance;
                if (existing.BuffTypeId != b.BuffTypeId
                    || existing.EndServerTick != SyncTickHelper.EndTick(EntityManager, (float)b.Remaining)
                    || existing.StackCount != b.Stacks
                    || existing.MaxStackCount != b.MaxStacks
                    || existing.SourceNetId != b.SourceUnitId
                    || existing.DamageType != (byte)b.DamageType) {
                    changed = true;
                    break;
                }
            }
        }
        if (!changed)
            return;

        while (BuffsList.Count > 0)
            BuffsList.RemoveAt(BuffsList.Count - 1);
        foreach (var buff in buffs)
            BuffsList.Add(new SyncBuffData {
                BuffTypeId = buff.Instance.BuffTypeId,
                EndServerTick = SyncTickHelper.EndTick(EntityManager, (float)buff.Instance.Remaining),
                StackCount = (ushort)buff.Instance.Stacks,
                MaxStackCount = (ushort)Math.Max(1, buff.Instance.MaxStacks),
                SourceNetId = buff.Instance.SourceUnitId,
                DamageType = (byte)buff.Instance.DamageType,
            });
    }

    /// <summary>仇恨全量投影，内容一致时跳过。在线端只下行不消费。</summary>
    private void ProjectHates(BattleUnit unit) {
        var hates = unit.Hates;
        bool changed = HatesList.Count != hates.Count;
        if (!changed) {
            for (int i = 0; i < hates.Count; i++) {
                var existing = HatesList[i];
                if (existing.TargetNetId != hates[i].TargetUnitId
                    || existing.HateValue != hates[i].Value) {
                    changed = true;
                    break;
                }
            }
        }
        if (!changed)
            return;

        while (HatesList.Count > 0)
            HatesList.RemoveAt(HatesList.Count - 1);
        foreach (var hate in hates)
            HatesList.Add(new SyncHateData { TargetNetId = hate.TargetUnitId, HateValue = hate.Value });
    }

    /// <summary>
    /// 冷却整包还原为领域条目：指纹变化才重建，指纹未变只逐条原地刷新剩余秒。
    /// 条目截止 tick 在冷却期内恒定，剩余秒是按本端插值 ServerTick 的派生值，不刷新即倒计时冻结。
    /// </summary>
    private void ApplyCooldowns(BattleUnit unit) {
        var snapshot = SkillCooldowns.Value;
        List<CooldownEntry> cooldowns = unit.RuntimeState.Cooldowns;
        int stamp = CooldownStamp(snapshot);
        int count = snapshot?.Entries.Count ?? 0;
        // 指纹含条目数，一致则索引一一对应；条目数不等说明列表被本地改写，走重建
        if (stamp == _cooldownStamp && cooldowns.Count == count) {
            if (snapshot != null)
                for (int i = 0; i < count; i++)
                    cooldowns[i].Remaining = SyncTickHelper.RemainingSeconds(EntityManager, snapshot.Entries[i].EndServerTick);
            return;
        }
        _cooldownStamp = stamp;

        cooldowns.Clear();
        if (snapshot == null)
            return;
        foreach (var entry in snapshot.Entries)
            cooldowns.Add(new CooldownEntry(new SkillKeyId(entry.SkillId),
                SyncTickHelper.RemainingSeconds(EntityManager, entry.EndServerTick)));
    }

    /// <summary>
    /// 全局冷却组整包还原为领域条目：指纹变化才重建，指纹未变只逐条原地刷新剩余秒。
    /// 条目截止 tick 在冷却期内恒定，剩余秒是按本端插值 ServerTick 的派生值，不刷新即倒计时冻结。
    /// </summary>
    private void ApplyGcds(BattleUnit unit) {
        var snapshot = Gcds.Value;
        List<GcdEntry> gcds = unit.RuntimeState.Gcds;
        int stamp = GcdStamp(snapshot);
        int count = snapshot?.Entries.Count ?? 0;
        if (stamp == _gcdStamp && gcds.Count == count) {
            if (snapshot != null)
                for (int i = 0; i < count; i++)
                    gcds[i].Remaining = SyncTickHelper.RemainingSeconds(EntityManager, snapshot.Entries[i].EndServerTick);
            return;
        }
        _gcdStamp = stamp;

        gcds.Clear();
        if (snapshot == null)
            return;
        foreach (var entry in snapshot.Entries)
            gcds.Add(new GcdEntry(entry.GroupKey,
                SyncTickHelper.RemainingSeconds(EntityManager, entry.EndServerTick)));
    }

    /// <summary>
    /// Buff 列表还原为 <see cref="ActiveBuff"/> 展示壳：指纹变化才重建，指纹未变只原地刷新剩余秒。
    /// 在线端不推进 Buff，到期条目随服务端下行增删。
    /// </summary>
    private void ApplyBuffs(BattleUnit unit) {
        var buffs = unit.RuntimeState.Buffs;
        int stamp = BuffStamp(BuffsList);
        // 同 ApplyCooldowns：截止时间是常量、剩余秒是派生值，跳过重建不等于跳过刷新
        if (stamp == _buffStamp && buffs.Count == BuffsList.Count) {
            for (int i = 0; i < buffs.Count; i++)
                buffs[i].Instance.Remaining = SyncTickHelper.RemainingSeconds(EntityManager, BuffsList[i].EndServerTick);
            return;
        }
        _buffStamp = stamp;

        buffs.Clear();
        foreach (var data in BuffsList)
            buffs.Add(new ActiveBuff(
                new BuffInstance {
                    BuffTypeId = data.BuffTypeId,
                    TargetUnitId = unit.UnitId,
                    SourceUnitId = data.SourceNetId,
                    Stacks = data.StackCount,
                    MaxStacks = data.MaxStackCount,
                    DamageType = (DamageType)data.DamageType,
                    Remaining = SyncTickHelper.RemainingSeconds(EntityManager, data.EndServerTick),
                },
                NetworkBuffDefinition.Instance,
                NoOpBuffEffect.Instance));
    }

    /// <summary>Buff 列表内容指纹，覆盖全部展示字段。</summary>
    private static int BuffStamp(SyncList<SyncBuffData> buffs) {
        var hash = new HashCode();
        hash.Add(buffs.Count);
        foreach (var b in buffs) {
            hash.Add(b.BuffTypeId);
            hash.Add(b.EndServerTick);
            hash.Add(b.StackCount);
            hash.Add(b.MaxStackCount);
            hash.Add(b.SourceNetId);
            hash.Add(b.DamageType);
        }
        return hash.ToHashCode();
    }

    /// <summary>冷却整包内容指纹，含空快照。</summary>
    private static int CooldownStamp(SyncSkillCooldownSnapshot? snapshot) {
        var hash = new HashCode();
        if (snapshot == null)
            return hash.ToHashCode();
        hash.Add(snapshot.Entries.Count);
        foreach (var entry in snapshot.Entries) {
            hash.Add(entry.SkillId);
            hash.Add(entry.EndServerTick);
        }
        return hash.ToHashCode();
    }

    /// <summary>全局冷却组整包内容指纹，含空快照。</summary>
    private static int GcdStamp(SyncGcdSnapshot? snapshot) {
        var hash = new HashCode();
        if (snapshot == null)
            return hash.ToHashCode();
        hash.Add(snapshot.Entries.Count);
        foreach (var entry in snapshot.Entries) {
            hash.Add(entry.GroupKey);
            hash.Add(entry.EndServerTick);
        }
        return hash.ToHashCode();
    }
}
