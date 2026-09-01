using LiteNetLib.Utils;

namespace DungeonChessBattle.Battle.Entities.Requests;

/// <summary>
/// 客户端请求设置聚焦目标的事件载荷，经可靠请求通道送抵服务端，
/// 服务端在输入门内校验后写权威领域态 BattleUnit.FocusTarget。
/// </summary>
public struct SetFocusTargetRequest : INetSerializable {
    /// <summary>目标单位网络 ID，0 表示清除聚焦目标。</summary>
    public ushort TargetNetId;

    /// <inheritdoc />
    public readonly void Serialize(NetDataWriter writer) => writer.Put(TargetNetId);

    /// <inheritdoc />
    public void Deserialize(NetDataReader reader) => TargetNetId = reader.GetUShort();
}
