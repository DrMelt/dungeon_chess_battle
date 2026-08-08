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

    /// <summary>伤害量（正）或治疗量（负）。纯 Buff 类技能为 0</summary>
    public float DamageOrCureValue;

    /// <summary>位置目标 X（范围伤害技能使用，XZ 平面）。</summary>
    public float TargetPosX;

    /// <summary>位置目标 Z（范围伤害技能使用，XZ 平面）。</summary>
    public float TargetPosZ;

    /// <summary>伤害类型（仅 IsDamage=true 时有效），对应 DamageType 转 byte</summary>
    public byte DamageType;

    /// <summary>true 为伤害技能，false 为治疗/Buff 技能</summary>
    public bool IsDamage;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2 + 2 + 2 + 4 + 4 + 4 + 1 + 1;

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(CasterUnitNetId);
        writer.Put(TargetUnitNetId);
        writer.Put(SkillTypeId);
        writer.Put(DamageOrCureValue);
        writer.Put(TargetPosX);
        writer.Put(TargetPosZ);
        writer.Put(DamageType);
        writer.Put(IsDamage);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        CasterUnitNetId = reader.GetUShort();
        TargetUnitNetId = reader.GetUShort();
        SkillTypeId = reader.GetUShort();
        DamageOrCureValue = reader.GetFloat();
        TargetPosX = reader.GetFloat();
        TargetPosZ = reader.GetFloat();
        DamageType = reader.GetByte();
        IsDamage = reader.GetBool();
    }
}
