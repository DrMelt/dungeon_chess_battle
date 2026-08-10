using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 技能施放请求的扁平化同步结构，用于 RPC 参数。
/// </summary>
public struct SyncSkillRequest : ISpanSerializable {
    /// <summary>施法单位 NetId</summary>
    public ushort CasterUnitNetId;

    /// <summary>目标单位 NetId</summary>
    public ushort TargetUnitNetId;

    /// <summary>技能类型 ID（对应配置表）</summary>
    public ushort SkillTypeId;

    /// <summary>位置目标 X（范围伤害技能使用，XZ 平面）。</summary>
    public float TargetPosX;

    /// <summary>位置目标 Z（范围伤害技能使用，XZ 平面）。</summary>
    public float TargetPosZ;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2 + 2 + 2 + 4 + 4;

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(CasterUnitNetId);
        writer.Put(TargetUnitNetId);
        writer.Put(SkillTypeId);
        writer.Put(TargetPosX);
        writer.Put(TargetPosZ);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        CasterUnitNetId = reader.GetUShort();
        TargetUnitNetId = reader.GetUShort();
        SkillTypeId = reader.GetUShort();
        TargetPosX = reader.GetFloat();
        TargetPosZ = reader.GetFloat();
    }
}
