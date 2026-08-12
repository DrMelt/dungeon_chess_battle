using DungeonChessBattle.Battle.Domain.Enums;
using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 创建单位的扁平化同步结构，用于 RPC 参数。
/// </summary>
public struct SyncCreateUnitRequest : ISpanSerializable {
    /// <summary>单位显示名称，如 "White Mage"。</summary>
    public string UnitName;

    /// <summary>阵营字符串标识，见 <see cref="CampConstants"/>。</summary>
    public string Camp;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 256 + 2 + 32; // max 256 chars UnitName + ushort length prefix + 32 chars Camp

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(UnitName, 256);
        writer.Put(Camp, 32);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        UnitName = reader.GetString();
        Camp = reader.GetString();
    }
}
