using DungeonChessBattle.Server.DataStore.Shared;

namespace DungeonChessBattle.Server.DataStore;

/// <summary>
/// 基于进程内字典的回放存储实现：roomId 主表与玩家记录主键索引。
/// 保留最近 <see cref="MaxReplays"/> 场，超出移除最旧归档，避免长期运行失控。
/// </summary>
public sealed class InMemoryReplayStore : IReplayStore {
    /// <summary>内存保留的最大回放场数。</summary>
    public const int MaxReplays = 256;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (ReplaySummary Summary, byte[] Data)> _replays = [];
    private readonly Dictionary<string, List<string>> _recordIndex = [];
    private readonly List<string> _order = [];

    /// <inheritdoc />
    public void Add(string roomId, ReplaySummary summary, byte[] data) {
        lock (_lock) {
            if (_replays.ContainsKey(roomId))
                return;

            _replays[roomId] = (summary, data);
            foreach (var player in summary.Players) {
                if (!_recordIndex.TryGetValue(player.PlayerRecordId, out var rooms)) {
                    rooms = [];
                    _recordIndex[player.PlayerRecordId] = rooms;
                }
                rooms.Add(roomId);
            }
            _order.Add(roomId);

            while (_order.Count > MaxReplays) {
                string oldest = _order[0];
                _order.RemoveAt(0);
                if (_replays.Remove(oldest, out var removed)) {
                    foreach (var player in removed.Summary.Players) {
                        if (_recordIndex.TryGetValue(player.PlayerRecordId, out var rooms)) {
                            rooms.Remove(oldest);
                            if (rooms.Count == 0)
                                _recordIndex.Remove(player.PlayerRecordId);
                        }
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ReplaySummary> GetReplaysByPlayerId(string recordId) {
        lock (_lock) {
            if (!_recordIndex.TryGetValue(recordId, out var rooms))
                return [];
            var result = new List<ReplaySummary>(rooms.Count);
            for (int i = rooms.Count - 1; i >= 0; i--)
                if (_replays.TryGetValue(rooms[i], out var entry))
                    result.Add(entry.Summary);
            return result;
        }
    }

    /// <inheritdoc />
    public bool TryGetReplay(string roomId, out byte[] data) {
        lock (_lock) {
            if (_replays.TryGetValue(roomId, out var entry)) {
                data = entry.Data;
                return true;
            }
            data = null!;
            return false;
        }
    }
}
