using MessagePack;

namespace DungeonChessBattle.Entities.Replay;

/// <summary>
/// 回放记录二进制编解码唯一权威，服务端导出与客户端下载解析共用。
/// 基于 MessagePack 序列化，模型以显式 Key 索引标注，字段重命名与顺序调整不破坏兼容；
/// 格式版本由 <see cref="ReplayFormatVersion"/> 门控，解码时校验。
/// </summary>
public static class ReplayRecordCoder {
    /// <summary>编码完整回放记录为字节流。</summary>
    public static byte[] Encode(ReplayRecordSnapshot snapshot)
        => MessagePackSerializer.Serialize(snapshot);

    /// <summary>从字节流解码回放记录；格式版本不匹配抛异常。</summary>
    public static ReplayRecordSnapshot Decode(ReadOnlyMemory<byte> data) {
        var snapshot = MessagePackSerializer.Deserialize<ReplayRecordSnapshot>(data);
        if (snapshot.Header.FormatVersion != ReplayFormatVersion.Current)
            throw new InvalidDataException($"Unsupported replay format version: {snapshot.Header.FormatVersion}.");
        return snapshot;
    }
}
