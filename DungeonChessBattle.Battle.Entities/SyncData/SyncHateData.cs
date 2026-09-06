using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 仇恨数据的扁平化同步结构，用于 SyncList 传输。
/// </summary>
public struct SyncHateData : ISpanSerializable {
    /// <summary>目标单位的网络 ID。</summary>
    public ushort TargetNetId {
        get; set;
    }

    /// <summary>仇恨值。</summary>
    public float HateValue {
        get; set;
    }

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2 + 4; // 6 bytes

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(TargetNetId);
        writer.Put(HateValue);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        TargetNetId = reader.GetUShort();
        HateValue = reader.GetFloat();
    }
}
