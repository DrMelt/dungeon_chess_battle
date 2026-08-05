using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// Buff 的扁平化同步数据，实现 ISpanSerializable 以便在 SyncList 中传输。
/// </summary>
public struct SyncBuffData : ISpanSerializable {
    /// <summary>Buff 类型 ID，对应配置表中的 Buff 名称哈希</summary>
    public ushort BuffTypeId;

    /// <summary>剩余持续时间（秒）</summary>
    public float RemainingDuration;

    /// <summary>每跳间隔（秒），0 表示非周期性 Buff</summary>
    public float TickInterval;

    /// <summary>每跳数值。DOT 为正（伤害量），HOT 为负（治疗量）。非周期性 Buff 为 0</summary>
    public float TickValue;

    /// <summary>当前叠加层数</summary>
    public ushort StackCount;

    /// <summary>最大叠加层数</summary>
    public ushort MaxStackCount;

    /// <summary>来源施法单位的 NetId</summary>
    public ushort SourceUnitNetId;

    /// <summary>伤害类型（仅 DOT 有效，HOT 和纯 Buff 忽略）</summary>
    public byte DamageType;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2 + 4 + 4 + 4 + 2 + 2 + 2 + 1; // 21 bytes

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(BuffTypeId);
        writer.Put(RemainingDuration);
        writer.Put(TickInterval);
        writer.Put(TickValue);
        writer.Put(StackCount);
        writer.Put(MaxStackCount);
        writer.Put(SourceUnitNetId);
        writer.Put(DamageType);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        BuffTypeId = reader.GetUShort();
        RemainingDuration = reader.GetFloat();
        TickInterval = reader.GetFloat();
        TickValue = reader.GetFloat();
        StackCount = reader.GetUShort();
        MaxStackCount = reader.GetUShort();
        SourceUnitNetId = reader.GetUShort();
        DamageType = reader.GetByte();
    }

    /// <summary>
    /// 判断此 Buff 是否为 DOT（持续伤害）
    /// </summary>
    public readonly bool IsDOT => TickInterval > 0 && TickValue > 0;

    /// <summary>
    /// 判断此 Buff 是否为 HOT（持续治疗）
    /// </summary>
    public readonly bool IsHOT => TickInterval > 0 && TickValue < 0;

    /// <summary>
    /// 判断此 Buff 是否可叠加
    /// </summary>
    public readonly bool IsStackable => MaxStackCount > 1;
}
