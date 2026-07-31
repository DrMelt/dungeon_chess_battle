using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 创建单位的扁平化同步结构，用于 RPC 参数。
/// </summary>
public struct SyncCreateUnitRequest : ISpanSerializable {
    /// <summary>单位显示名称（如 "White Mage"）</summary>
    public string UnitName;

    /// <summary>阵营：1=Camp_A, 2=Camp_B</summary>
    public byte Camp;

    public readonly int MaxSize => 256 + 1; // max 256 bytes UTF-8 + 1 byte camp

    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(UnitName, 256);
        writer.Put(Camp);
    }

    public void Deserialize(ref SpanReader reader) {
        UnitName = reader.GetString();
        Camp = reader.GetByte();
    }
}
