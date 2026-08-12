using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 设置聚焦目标的扁平化同步结构，用于 RPC 参数。
/// </summary>
public struct SyncFocusTargetRequest : ISpanSerializable {
    /// <summary>目标单位的网络 ID，0 表示清除聚焦目标。</summary>
    public ushort TargetUnitNetId;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2;

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(TargetUnitNetId);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        TargetUnitNetId = reader.GetUShort();
    }
}