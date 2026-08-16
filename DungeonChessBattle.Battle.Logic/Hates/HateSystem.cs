using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Logic.Hates;

/// <summary>
/// 服务端权威仇恨账本：每单位一张对目标的仇恨表。
/// 以网络 ID 为键，不持有单位引用，纯数据结构可独立单测。
/// 增删改查与 dirty 标记，投影到 IBattleUnit.Hates 由 BattleEngine 驱动。
/// </summary>
public sealed class HateSystem {
    /// <summary>持有者网络 ID → 目标网络 ID → 仇恨值。</summary>
    private readonly Dictionary<ushort, Dictionary<ushort, float>> _hates = [];

    /// <summary>仇恨表发生变化的持有者集合，供投影节流。</summary>
    private readonly HashSet<ushort> _dirty = [];

    /// <summary>仇恨退场阈值：低于该值视为无仇恨并移除条目，防止脏条目堆积。</summary>
    private const float Epsilon = 0.01f;

    /// <summary>应用单个仇恨修改效果到账本，负增量将按阈值裁剪条目。</summary>
    public void ApplyEffect(HateEffect effect) {
        switch (effect.Op) {
            case HateEffectOp.Add:
                Add(effect.HolderNetId, effect.SourceNetId, effect.Value);
                break;
            case HateEffectOp.Multiply:
                Multiply(effect.HolderNetId, effect.SourceNetId, effect.Value);
                break;
            case HateEffectOp.SetTop:
                SetTop(effect.HolderNetId, effect.SourceNetId, effect.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Op, "Unknown hate effect op.");
        }
    }

    /// <summary>持有者仇恨表对目标增量加指定值，内部处理裁剪；零增量直接返回不落账。</summary>
    public void Add(ushort holderNetId, ushort targetNetId, float value) {
        var table = TableOf(holderNetId);
        if (value == 0f)
            return;
        float updated = table.GetValueOrDefault(targetNetId) + value;
        SetInternal(table, targetNetId, updated);
        _dirty.Add(holderNetId);
    }

    /// <summary>持有者仇恨表对目标乘倍率，目标不存在时忽略。</summary>
    public void Multiply(ushort holderNetId, ushort targetNetId, float multiplier) {
        var table = TableOf(holderNetId);
        if (!table.TryGetValue(targetNetId, out float current))
            return;
        SetInternal(table, targetNetId, current * multiplier);
        _dirty.Add(holderNetId);
    }

    /// <summary>把持有者仇恨表中对目标的仇恨抬到当前最高之上指定余量，实现嘲讽。</summary>
    public void SetTop(ushort holderNetId, ushort targetNetId, float overage) {
        var table = TableOf(holderNetId);
        float top = table.Count == 0 ? 0f : table.Values.Max();
        SetInternal(table, targetNetId, top + overage);
        _dirty.Add(holderNetId);
    }

    private static Dictionary<ushort, float> TableOf(Dictionary<ushort, Dictionary<ushort, float>> hates,
        ushort holderNetId) {
        if (!hates.TryGetValue(holderNetId, out var table)) {
            table = [];
            hates[holderNetId] = table;
        }
        return table;
    }

    private Dictionary<ushort, float> TableOf(ushort holderNetId) => TableOf(_hates, holderNetId);

    private static void SetInternal(Dictionary<ushort, float> table, ushort targetNetId, float value) {
        if (value <= Epsilon)
            table.Remove(targetNetId);
        else
            table[targetNetId] = value;
    }

    /// <summary>查询持有者对目标的仇恨值，无条目返回 0。</summary>
    public float ValueOf(ushort holderNetId, ushort targetNetId) {
        return _hates.TryGetValue(holderNetId, out var table) && table.TryGetValue(targetNetId, out float value)
            ? value
            : 0f;
    }

    /// <summary>清空单位的整张仇恨表，并把它从所有其他持有者的表中移除，目标死亡时调用。</summary>
    public void RemoveUnit(ushort netId) {
        _hates.Remove(netId);
        _dirty.Remove(netId);
        foreach (var (holder, table) in _hates) {
            if (!table.Remove(netId))
                continue;
            _dirty.Add(holder);
        }
    }

    /// <summary>取出并清空所有脏持有者，供 BattleEngine 统一投影同步。</summary>
    public List<ushort> GetDirtyAndClear() {
        var result = new List<ushort>(_dirty);
        _dirty.Clear();
        return result;
    }

    /// <summary>持有者仇恨快照，按仇恨值降序。</summary>
    public IReadOnlyList<HateSnapshot> Snapshot(ushort holderNetId) {
        if (!_hates.TryGetValue(holderNetId, out var table))
            return [];
        return [.. table.OrderByDescending(kv => kv.Value).Select(kv => new HateSnapshot(kv.Key, kv.Value))];
    }
}
