using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 仇恨数据的扁平化同步结构，用于 SyncList 传输。
/// </summary>
public struct SyncHateData : ISpanSerializable
{
    /// <summary>目标单位的 NetId</summary>
    public ushort TargetUnitNetId;

    /// <summary>仇恨值</summary>
    public float HateValue;

    public int MaxSize => 2 + 4; // 6 bytes

    public void Serialize(ref SpanWriter writer)
    {
        writer.Put(TargetUnitNetId);
        writer.Put(HateValue);
    }

    public void Deserialize(ref SpanReader reader)
    {
        TargetUnitNetId = reader.GetUShort();
        HateValue = reader.GetFloat();
    }
}