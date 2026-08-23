namespace DungeonChessBattle.Battle.Domain.Combat.Hates;

/// <summary>
/// 单位权威仇恨表：本单位对各目标的仇恨账本，由 <see cref="UnitCombatState.Hates"/> 持有。
/// 以目标网络 ID 为键，不持有单位引用，纯数据结构可独立单测。
/// 增删改查，变更经投影器内容比较节流同步。
/// </summary>
public sealed class HateTable {
    /// <summary>目标网络 ID → 仇恨值。</summary>
    private readonly Dictionary<ushort, float> _hates = [];

    /// <summary>仇恨退场阈值：低于该值视为无仇恨并移除条目，防止脏条目堆积。</summary>
    private const float Epsilon = 0.01f;

    /// <summary>应用单个仇恨修改效果，负增量将按阈值裁剪条目。</summary>
    public void ApplyEffect(HateEffect effect) {
        switch (effect.Op) {
            case HateEffectOp.Add:
                Add(effect.SourceNetId, effect.Value);
                break;
            case HateEffectOp.Multiply:
                Multiply(effect.SourceNetId, effect.Value);
                break;
            case HateEffectOp.SetTop:
                SetTop(effect.SourceNetId, effect.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Op, "Unknown hate effect op.");
        }
    }

    /// <summary>对目标增量加指定值，内部处理裁剪；零增量直接返回不落账。</summary>
    public void Add(ushort targetNetId, float value) {
        if (value == 0f)
            return;
        SetInternal(targetNetId, _hates.GetValueOrDefault(targetNetId) + value);
    }

    /// <summary>对目标乘倍率，目标不存在时忽略。</summary>
    public void Multiply(ushort targetNetId, float multiplier) {
        if (!_hates.TryGetValue(targetNetId, out float current))
            return;
        SetInternal(targetNetId, current * multiplier);
    }

    /// <summary>把对目标的仇恨抬到当前最高之上指定余量，实现嘲讽。</summary>
    public void SetTop(ushort targetNetId, float overage) {
        float top = _hates.Count == 0 ? 0f : _hates.Values.Max();
        SetInternal(targetNetId, top + overage);
    }

    /// <summary>移除对目标的仇恨条目，目标不存在时忽略；供单位死亡清理调用。</summary>
    public void RemoveTarget(ushort targetNetId) {
        _hates.Remove(targetNetId);
    }

    /// <summary>清空整张仇恨表，供单位死亡清理调用。</summary>
    public void Clear() {
        _hates.Clear();
    }

    private void SetInternal(ushort targetNetId, float value) {
        if (value <= Epsilon)
            _hates.Remove(targetNetId);
        else
            _hates[targetNetId] = value;
    }

    /// <summary>查询对目标的仇恨值，无条目返回 0。</summary>
    public float ValueOf(ushort targetNetId) => _hates.GetValueOrDefault(targetNetId);

    /// <summary>仇恨快照，按仇恨值降序。</summary>
    public IReadOnlyList<HateSnapshot> Snapshot() =>
        [.. _hates.OrderByDescending(kv => kv.Value).Select(kv => new HateSnapshot(kv.Key, kv.Value))];
}
