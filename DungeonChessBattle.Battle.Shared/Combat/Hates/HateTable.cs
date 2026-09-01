namespace DungeonChessBattle.Battle.Shared.Combat.Hates;

/// <summary>
/// 单位权威仇恨表：本单位对各目标的仇恨账本，由 <see cref="UnitCombatState.Hates"/> 持有。
/// 以目标单位为键，不持有单位引用，纯数据结构可独立单测。
/// 增删改查，变更经状态同步器内容比较节流同步。
/// </summary>
public sealed class HateTable {
    /// <summary>目标单位 → 仇恨值。</summary>
    private readonly Dictionary<UnitId, float> _hates = [];

    /// <summary>仇恨退场阈值：低于该值视为无仇恨并移除条目，防止脏条目堆积。</summary>
    private const float Epsilon = 0.01f;

    /// <summary>应用单个仇恨修改效果，负增量将按阈值裁剪条目。</summary>
    public void ApplyEffect(HateEffect effect) {
        switch (effect.Op) {
            case HateEffectOp.Add:
                Add(effect.SourceUnitId, effect.Value);
                break;
            case HateEffectOp.Multiply:
                Multiply(effect.SourceUnitId, effect.Value);
                break;
            case HateEffectOp.SetTop:
                SetTop(effect.SourceUnitId, effect.Value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(effect), effect.Op, "Unknown hate effect op.");
        }
    }

    /// <summary>对目标增量加指定值，内部处理裁剪；零增量直接返回不落账。</summary>
    public void Add(UnitId targetUnitId, float value) {
        if (value == 0f)
            return;
        SetInternal(targetUnitId, _hates.GetValueOrDefault(targetUnitId) + value);
    }

    /// <summary>对目标乘倍率，目标不存在时忽略。</summary>
    public void Multiply(UnitId targetUnitId, float multiplier) {
        if (!_hates.TryGetValue(targetUnitId, out float current))
            return;
        SetInternal(targetUnitId, current * multiplier);
    }

    /// <summary>把对目标的仇恨抬到当前最高之上指定余量，实现嘲讽。</summary>
    public void SetTop(UnitId targetUnitId, float overage) {
        float top = _hates.Count == 0 ? 0f : _hates.Values.Max();
        SetInternal(targetUnitId, top + overage);
    }

    /// <summary>移除对目标的仇恨条目，目标不存在时忽略；供单位死亡清理调用。</summary>
    public void RemoveTarget(UnitId targetUnitId) {
        _hates.Remove(targetUnitId);
    }

    /// <summary>清空整张仇恨表，供单位死亡清理调用。</summary>
    public void Clear() {
        _hates.Clear();
    }

    private void SetInternal(UnitId targetUnitId, float value) {
        if (value <= Epsilon)
            _hates.Remove(targetUnitId);
        else
            _hates[targetUnitId] = value;
    }

    /// <summary>查询对目标的仇恨值，无条目返回 0。</summary>
    public float ValueOf(UnitId targetUnitId) => _hates.GetValueOrDefault(targetUnitId);

    /// <summary>仇恨快照，按仇恨值降序。</summary>
    public IReadOnlyList<HateSnapshot> Snapshot() =>
        [.. _hates.OrderByDescending(kv => kv.Value).Select(kv => new HateSnapshot(kv.Key, kv.Value))];
}
