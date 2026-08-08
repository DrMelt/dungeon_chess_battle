using LiteEntitySystem;

namespace DungeonChessBattle.Entities.SyncData;

/// <summary>
/// 技能个体冷却的扁平化同步数据，实现 ISpanSerializable 以便在 SyncList 中传输。
/// 服务端权威写入，客户端仅读剩余秒数用于冷却按钮 UI。
/// </summary>
public struct SyncSkillCooldown : ISpanSerializable {
    /// <summary>技能配置 ID。</summary>
    public ushort SkillId;

    /// <summary>剩余冷却秒数。</summary>
    public float Remaining;

    /// <summary>序列化后的最大字节数。</summary>
    public readonly int MaxSize => 2 + 4; // 6 bytes

    /// <summary>
    /// 序列化到网络缓冲区。
    /// </summary>
    /// <param name="writer">序列化写入器。</param>
    public readonly void Serialize(ref SpanWriter writer) {
        writer.Put(SkillId);
        writer.Put(Remaining);
    }

    /// <summary>
    /// 从网络缓冲区反序列化。
    /// </summary>
    /// <param name="reader">序列化读取器。</param>
    public void Deserialize(ref SpanReader reader) {
        SkillId = reader.GetUShort();
        Remaining = reader.GetFloat();
    }
}
