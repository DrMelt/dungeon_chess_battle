using LiteNetLib.Utils;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// Buff 列表的整包同步数据，经 SyncNetSerializable 以 LiteNetLib 变长序列化传输。
/// 条目数动态，Buff 键以字符串序列化；服务端仅在条目内容变化时整包重建，客户端按当前服务器 tick 本地推算剩余秒数。
/// 键必须是字符串，故本载荷是 class 而非 unmanaged struct——SyncList 装不下引用字段。
/// </summary>
public sealed class SyncBuffSnapshot : INetSerializable {
    /// <summary>单条 Buff：键、截止服务器逻辑 tick、层数、来源与伤害类型。</summary>
    public readonly record struct Entry(
        string BuffKey,
        ushort EndServerTick,
        ushort StackCount,
        ushort SourceNetId,
        byte DamageType);

    private readonly List<Entry> _entries = [];

    /// <summary>当前 Buff 条目只读视图。</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>整包覆盖 Buff 条目。</summary>
    public void Set(IEnumerable<Entry> entries) {
        _entries.Clear();
        _entries.AddRange(entries);
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer) {
        writer.Put(_entries.Count);
        foreach (var entry in _entries) {
            writer.Put(entry.BuffKey);
            writer.Put(entry.EndServerTick);
            writer.Put(entry.StackCount);
            writer.Put(entry.SourceNetId);
            writer.Put(entry.DamageType);
        }
    }

    /// <inheritdoc />
    public void Deserialize(NetDataReader reader) {
        int count = reader.GetInt();
        _entries.Clear();
        for (int i = 0; i < count; i++)
            _entries.Add(new Entry(
                reader.GetString(), reader.GetUShort(), reader.GetUShort(),
                reader.GetUShort(), reader.GetByte()));
    }
}
