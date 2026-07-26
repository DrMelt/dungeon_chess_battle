using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 技能施放请求的扁平化同步结构，用于 RPC 参数。
/// </summary>
public struct SyncSkillRequest : ISpanSerializable
{
    /// <summary>施法单位 NetId</summary>
    public ushort CasterUnitNetId;

    /// <summary>目标单位 NetId</summary>
    public ushort TargetUnitNetId;

    /// <summary>技能类型 ID（对应配置表）</summary>
    public ushort SkillTypeId;

    public int MaxSize => 2 + 2 + 2; // 6 bytes

    public void Serialize(ref SpanWriter writer)
    {
        writer.Put(CasterUnitNetId);
        writer.Put(TargetUnitNetId);
        writer.Put(SkillTypeId);
    }

    public void Deserialize(ref SpanReader reader)
    {
        CasterUnitNetId = reader.GetUShort();
        TargetUnitNetId = reader.GetUShort();
        SkillTypeId = reader.GetUShort();
    }
}