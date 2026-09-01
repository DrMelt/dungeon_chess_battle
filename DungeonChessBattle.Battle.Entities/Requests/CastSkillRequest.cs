using LiteNetLib.Utils;

namespace DungeonChessBattle.Battle.Entities.Requests;

/// <summary>
/// 客户端请求施放技能的事件载荷，实现 LiteNetLib INetSerializable 以便经
/// HumanControllerLogic 可靠请求通道送抵服务端。
/// 施法者不在此结构声明，服务端从请求来源控制器持有的单位推导，杜绝伪造施法者。
/// </summary>
public struct CastSkillRequest : INetSerializable {
    /// <summary>技能配置键。</summary>
    public string SkillKey;

    /// <summary>目标单位网络 ID，位置型技能为 0。</summary>
    public ushort TargetNetId;

    /// <summary>位置目标 X，范围伤害技能使用，世界坐标。</summary>
    public float TargetPosX;

    /// <summary>位置目标 Z，范围伤害技能使用，世界坐标。</summary>
    public float TargetPosZ;

    /// <inheritdoc />
    public readonly void Serialize(NetDataWriter writer) {
        writer.Put(SkillKey);
        writer.Put(TargetNetId);
        writer.Put(TargetPosX);
        writer.Put(TargetPosZ);
    }

    /// <inheritdoc />
    public void Deserialize(NetDataReader reader) {
        SkillKey = reader.GetString();
        TargetNetId = reader.GetUShort();
        TargetPosX = reader.GetFloat();
        TargetPosZ = reader.GetFloat();
    }
}
