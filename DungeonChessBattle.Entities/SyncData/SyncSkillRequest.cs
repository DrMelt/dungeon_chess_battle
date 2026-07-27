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

    /// <summary>伤害类型（仅 IsDamage=true 时有效），对应 Enum_DamageType 转 byte</summary>
    public byte DamageType;

    /// <summary>true 为伤害技能，false 为治疗/Buff 技能</summary>
    public bool IsDamage;

    public readonly int MaxSize => 2 + 2 + 2 + 4 + 1 + 1;

    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(CasterUnitNetId);
        writer.Put(TargetUnitNetId);
        writer.Put(SkillTypeId);
        writer.Put(DamageOrCureValue);
        writer.Put(DamageType);
        writer.Put(IsDamage);
    }

    public void Deserialize(ref SpanReader reader) {
        CasterUnitNetId = reader.GetUShort();
        TargetUnitNetId = reader.GetUShort();
        SkillTypeId = reader.GetUShort();
        DamageOrCureValue = reader.GetFloat();
        DamageType = reader.GetByte();
        IsDamage = reader.GetBool();
    }
}
