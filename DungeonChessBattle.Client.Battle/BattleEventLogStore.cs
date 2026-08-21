using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 当前房间会话的战斗事件日志仓库。只保存事件与接收时刻，不做格式化与显示。
/// 追加/读取/清空均发生在 Godot 主线程网络更新阶段，无需加锁。
/// 会话重置（断线/重连/离开）时由 RoomBattleClient 调用 Clear。
/// </summary>
public sealed class BattleEventLogStore {
    private readonly List<BattleEventLogEntry> _entries = [];
    private long _version;

    /// <summary>已保存的事件条数，供 UI 增量同步游标使用。</summary>
    public int Count => _entries.Count;

    /// <summary>会话版本号，Clear 会话重置时自增，UI 据此识别会话切换。</summary>
    public long Version => _version;

    /// <summary>追加一批事件，批次共享同一接收时刻。</summary>
    public void Append(IEnumerable<IBattleEvent> events, long receiveUnixMs) {
        foreach (var e in events)
            _entries.Add(new BattleEventLogEntry(receiveUnixMs, e));
    }

    /// <summary>已保存事件只读视图，调用方仅允许枚举。</summary>
    public IReadOnlyList<BattleEventLogEntry> Entries => _entries;

    /// <summary>清空全部日志并递增会话版本号，房间会话重置时调用。</summary>
    public void Clear() {
        _entries.Clear();
        _version++;
    }
}
