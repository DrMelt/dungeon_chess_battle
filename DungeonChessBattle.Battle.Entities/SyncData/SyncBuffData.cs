using LiteEntitySystem;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// Buff 的扁平化同步数据。RPC 广播与 SyncList 均以本结构传输。
/// 服务端权威写入截止 tick，客户端按当前服务器 tick 本地推算剩余时间。
/// </summary>
public struct SyncBuffData : ISpanSerializable {
    /// <summary>Buff 类型 ID，对应配置表中的 Buff 名称哈希。</summary>
    public ushort BuffTypeId;

    /// <summary>Buff 截止的服务器逻辑 tick，客户端据此本地推算剩余时间。</summary>
    public ushort EndServerTick;

    /// <summary>当前叠加层数。</summary>
    public ushort StackCount;

    /// <summary>最大叠加层数。</summary>
    public ushort MaxStackCount;

    /// <summary>来源施法单位的网络 ID。</summary>
    public ushort SourceNetId;

    /// <summary>伤害类型，仅 DOT 有效，HOT 和纯 Buff 忽略。</summary>
    public byte DamageType;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2 + 2 + 2 + 2 + 2 + 1; // 11 bytes

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(BuffTypeId);
        writer.Put(EndServerTick);
        writer.Put(StackCount);
        writer.Put(MaxStackCount);
        writer.Put(SourceNetId);
        writer.Put(DamageType);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        BuffTypeId = reader.GetUShort();
        EndServerTick = reader.GetUShort();
        StackCount = reader.GetUShort();
        MaxStackCount = reader.GetUShort();
        SourceNetId = reader.GetUShort();
        DamageType = reader.GetByte();
    }

    /// <summary>
    /// 判断此 Buff 是否可叠加
    /// </summary>
    public readonly bool IsStackable => MaxStackCount > 1;
}
