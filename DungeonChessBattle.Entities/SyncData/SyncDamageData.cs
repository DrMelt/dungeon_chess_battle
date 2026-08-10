using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 受击事件的扁平化同步结构，用于服务端→客户端 RPC 广播。
/// 打包伤害量与伤害类型（RPC 参数仅支持单一 ISpanSerializable 类型）。
/// </summary>
public struct SyncDamageData : ISpanSerializable {
    /// <summary>实际扣除的生命值。</summary>
    public float Damage;

    /// <summary>伤害类型（对应 DamageType 转 byte）。</summary>
    public byte DamageType;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 4 + 1;

    /// <summary>序列化到网络缓冲区。</summary>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(Damage);
        writer.Put(DamageType);
    }

    /// <summary>从网络缓冲区反序列化。</summary>
    public void Deserialize(ref SpanReader reader) {
        Damage = reader.GetFloat();
        DamageType = reader.GetByte();
    }
}
