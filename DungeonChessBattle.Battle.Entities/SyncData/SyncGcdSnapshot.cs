using LiteNetLib.Utils;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 全局冷却组列表的整包同步数据，经 SyncNetSerializable 以 LiteNetLib 变长序列化传输。
/// 条目数动态，组键以字符串序列化；服务端仅在条目内容变化时整包重建，客户端按当前服务器 tick 本地推算剩余秒数。
/// </summary>
public sealed class SyncGcdSnapshot : INetSerializable {
    /// <summary>单条全局冷却组：组键与截止服务器逻辑 tick。</summary>
    public readonly record struct Entry(string GroupKey, ushort EndServerTick);

    private readonly List<Entry> _entries = [];

    /// <summary>当前全局冷却组条目只读视图。</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>整包覆盖全局冷却组条目。</summary>
    public void Set(IEnumerable<Entry> entries) {
        _entries.Clear();
        _entries.AddRange(entries);
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer) {
        writer.Put(_entries.Count);
        foreach (var entry in _entries) {
            writer.Put(entry.GroupKey);
            writer.Put(entry.EndServerTick);
        }
    }

    /// <inheritdoc />
    public void Deserialize(NetDataReader reader) {
        int count = reader.GetInt();
        _entries.Clear();
        for (int i = 0; i < count; i++)
            _entries.Add(new Entry(reader.GetString(), reader.GetUShort()));
    }
}
