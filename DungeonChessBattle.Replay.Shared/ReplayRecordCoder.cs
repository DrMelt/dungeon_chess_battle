using MessagePack;

namespace DungeonChessBattle.Replay.Shared;

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

    /// <summary>
    /// 只读记录头部：快照以数组编码且 Header 是第 0 个元素，读掉数组头与第一个元素即止，
    /// 不触碰后续输入条目，因此本地缓存枚举元数据只需读文件前缀而不必整包解码。
    /// 刻意不校验 FormatVersion——枚举只用于列表展示，能否重放由 <see cref="Decode"/> 门控。
    /// 数据不完整（前缀装不下头部）或结构不符返回 false。
    /// </summary>
    public static bool TryReadHeader(ReadOnlyMemory<byte> data, out ReplayRecordHeader? header) {
        header = null;
        try {
            var reader = new MessagePackReader(data);
            if (reader.TryReadNil() || reader.ReadArrayHeader() < 1)
                return false;
            header = MessagePackSerializer.Deserialize<ReplayRecordHeader>(ref reader);
            return header is not null;
        }
        catch (Exception) {
            // 前缀截断、结构错位、外部塞入的无关文件都落在这里：枚举不是校验点
            return false;
        }
    }
}
