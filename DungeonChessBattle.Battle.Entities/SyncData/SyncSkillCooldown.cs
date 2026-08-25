using LiteNetLib.Utils;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 技能个体冷却列表的整包同步数据，经 SyncNetSerializable 以 LiteNetLib 变长序列化传输。
/// 条目数动态，技能键以字符串序列化；服务端整帧覆盖，客户端按当前服务器 tick 本地推算剩余秒数。
/// </summary>
public sealed class SyncSkillCooldownSnapshot : INetSerializable {
    /// <summary>单条冷却项：技能键与截止服务器逻辑 tick。</summary>
    public readonly record struct Entry(string SkillId, ushort EndServerTick);

    private readonly List<Entry> _entries = [];

    /// <summary>当前冷却条目只读视图。</summary>
    public IReadOnlyList<Entry> Entries => _entries;

    /// <summary>整包覆盖冷却条目。</summary>
    public void Set(IEnumerable<Entry> entries) {
        _entries.Clear();
        _entries.AddRange(entries);
    }

    /// <inheritdoc />
    public void Serialize(NetDataWriter writer) {
        writer.Put(_entries.Count);
        foreach (var entry in _entries) {
            writer.Put(entry.SkillId);
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

