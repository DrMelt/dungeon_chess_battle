using DungeonChessBattle.Server.Abstractions;

namespace DungeonChessBattle.Server.DataStore;

/// <summary>
/// 基于进程内字典的回放归档存储实现：roomId 主表存字节流，玩家记录主键索引存房间 ID。
/// 保留最近 <see cref="MaxReplays"/> 场，超出移除最旧归档，避免长期运行失控。
/// </summary>
public sealed class InMemoryReplayStore : IReplayStore {
    /// <summary>内存保留的最大回放场数。</summary>
    public const int MaxReplays = 256;

    private readonly Lock _lock = new();
    private readonly Dictionary<string, (byte[] Archive, string[] Participants)> _archives = [];
    private readonly Dictionary<string, List<string>> _recordIndex = [];
    private readonly List<string> _order = [];

    /// <inheritdoc />
    public void Add(string roomId, byte[] archive, IReadOnlyList<string> participantRecordIds) {
        lock (_lock) {
            if (_archives.ContainsKey(roomId))
                return;

            var participants = participantRecordIds.ToArray();
            _archives[roomId] = (archive, participants);
            foreach (string recordId in participants) {
                if (!_recordIndex.TryGetValue(recordId, out var rooms)) {
                    rooms = [];
                    _recordIndex[recordId] = rooms;
                }

                rooms.Add(roomId);
            }

            _order.Add(roomId);

            while (_order.Count > MaxReplays) {
                string oldest = _order[0];
                _order.RemoveAt(0);
                if (!_archives.Remove(oldest, out var evicted))
                    continue;

                // 反向索引按被淘汰归档自己的参与者清理，与新入库的那场无关
                foreach (string recordId in evicted.Participants) {
                    if (_recordIndex.TryGetValue(recordId, out var rooms)) {
                        rooms.Remove(oldest);
                        if (rooms.Count == 0)
                            _recordIndex.Remove(recordId);
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetRoomIdsByPlayer(string recordId) {
        lock (_lock) {
            if (!_recordIndex.TryGetValue(recordId, out var rooms))
                return [];
            var result = new List<string>(rooms.Count);
            for (int i = rooms.Count - 1; i >= 0; i--)
                if (_archives.ContainsKey(rooms[i]))
                    result.Add(rooms[i]);
            return result;
        }
    }

    /// <inheritdoc />
    public bool TryGetArchive(string roomId, out byte[] archive) {
        lock (_lock) {
            if (_archives.TryGetValue(roomId, out var entry)) {
                archive = entry.Archive;
                return true;
            }

            archive = [];
            return false;
        }
    }
}
