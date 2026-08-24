using System.Collections;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Logic.Events;

/// <summary>
/// 每帧战斗事件日志：处理开始 Clear，处理中只增追加，后续处理经只读视图读取。
/// 实现 IReadOnlyList 使 Tick 返回值、仇恨分发与网络外送沿用既有契约。
/// 日志仅当帧有效，调用方不得跨帧持有。
/// </summary>
public sealed class BattleEventLog : IReadOnlyList<IBattleEvent> {
    private readonly List<IBattleEvent> _events = [];

    /// <inheritdoc />
    public int Count => _events.Count;

    /// <inheritdoc />
    public IBattleEvent this[int index] => _events[index];

    /// <inheritdoc />
    public IEnumerator<IBattleEvent> GetEnumerator() => _events.GetEnumerator();

    /// <inheritdoc />
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>处理中追加一条事件。只增不删，禁止改动已追加条目。</summary>
    public void Append(IBattleEvent evt) => _events.Add(evt);

    /// <summary>批量汇入效果产出的事件。</summary>
    public void AppendRange(IEnumerable<IBattleEvent> events) => _events.AddRange(events);

    /// <summary>仅由 Tick 开头调用，重置本帧日志。</summary>
    public void Clear() => _events.Clear();
}
