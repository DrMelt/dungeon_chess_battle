using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using MessagePack;

namespace DungeonChessBattle.Replay.Shared;

/// <summary>
/// 回放容器的块类型。未知块读侧跳过：新增块只升 <see cref="ReplayArchive.MinorVersion"/>，
/// 既有块的语义变化才升 <see cref="ReplayArchive.FormatVersion"/>。
/// </summary>
public enum ReplayChunkType : ushort {
    /// <summary>元数据，恒为首块且不压缩，供前缀读取。</summary>
    Meta = 1,

    /// <summary>全部单位的初始态。</summary>
    UnitInit = 2,

    /// <summary>玩家移动轨道，按玩家分轨的方向意图段序列。</summary>
    MoveTrack = 3,

    /// <summary>施法请求条目。</summary>
    Cast = 4,

    /// <summary>聚焦请求条目。</summary>
    Focus = 5,

    /// <summary>关键帧快照：仅占号，写侧尚未产出，待战斗世界状态序列化落地。</summary>
    Keyframe = 6,
}

/// <summary>块负载的存储编码。</summary>
public enum ReplayChunkCodec : byte {
    /// <summary>原样存储。</summary>
    Raw = 0,

    /// <summary>Deflate 压缩，块头声明解压后长度。</summary>
    Deflate = 1,
}

/// <summary>容器读取结论。</summary>
public enum ReplayArchiveStatus {
    /// <summary>读取成功。</summary>
    Ok,

    /// <summary>格式版本不由本机识别：结构不兼容，不是数据坏了。</summary>
    UnsupportedVersion,

    /// <summary>魔数、长度、校验或块结构不合：截断、位翻转或外部塞入的文件。</summary>
    Malformed,

    /// <summary>前缀装不下元数据块，还差多少由 RequiredBytes 给出。</summary>
    NeedMoreData,
}

/// <summary>整档解码结果。</summary>
/// <param name="Status">读取结论。</param>
/// <param name="Recording">解码出的回放内容，仅 <see cref="ReplayArchiveStatus.Ok"/> 时非空。</param>
/// <param name="Reason">失败原因，面向日志。</param>
public sealed record ReplayDecodeResult(
    ReplayArchiveStatus Status, ReplayRecording? Recording = null, string? Reason = null);

/// <summary>元数据前缀读取结果。</summary>
/// <param name="Status">读取结论。</param>
/// <param name="Meta">元数据，仅 <see cref="ReplayArchiveStatus.Ok"/> 时非空。</param>
/// <param name="RequiredBytes"><see cref="ReplayArchiveStatus.NeedMoreData"/> 时需要凑够的字节数。</param>
/// <param name="Reason">失败原因，面向日志。</param>
public sealed record ReplayMetaResult(
    ReplayArchiveStatus Status, ReplayMeta? Meta = null, int RequiredBytes = 0, string? Reason = null);

/// <summary>
/// 回放归档容器编解码唯一权威：字节流自包含、可前缀读元数据、逐块自校验，服务端归档与客户端解析共用。
/// 布局：
/// <code>
/// "DCBR"(4) | u16 FormatVersion | u16 MinorVersion
/// chunk*    : u16 Type | u8 Codec | u32 StoredLen | u32 RawLen | u32 Crc32 | payload
/// "DCBR"(4)
/// </code>
/// 块负载一律 MessagePack 显式 Key 模型，字段重命名与顺序调整不破坏兼容；重复字符串交给块级
/// Deflate 吃掉，不再单设字符串表。尾部魔数是完整性判据：半截下载与断文件在此分别，不必靠解码
/// 抛异常反推。校验和算在存储字节上，一并覆盖压缩与传输两段。
/// </summary>
public static class ReplayArchive {
    /// <summary>容器格式版本，块语义变化时递增。v6 起为分块容器，v5 及更早的单包 MessagePack 数组不再可读。</summary>
    public const int FormatVersion = 6;

    /// <summary>容器小版本，新增可跳过块时递增，读侧不据此拒绝。</summary>
    public const int MinorVersion = 0;

    /// <summary>容器头字节数：魔数加两个版本号。</summary>
    public const int HeaderBytes = 8;

    /// <summary>块头字节数：u16 类型 + u8 编码 + 三个 u32。</summary>
    public const int ChunkHeaderBytes = 15;

    /// <summary>容器尾字节数，魔数再落一份。</summary>
    public const int TrailerBytes = 4;

    /// <summary>读元数据的最小前缀：容器头加块头；真实需要量由 RequiredBytes 给出。</summary>
    public const int MetaProbeBytes = HeaderBytes + ChunkHeaderBytes;

    /// <summary>单块解压后上限，超出即判损坏，防压缩放大。</summary>
    public const int MaxChunkRawBytes = 32 * 1024 * 1024;

    private static ReadOnlySpan<byte> Magic => "DCBR"u8;

    /// <summary>把一场回放编码为归档字节流。</summary>
    public static byte[] Encode(ReplayRecording recording) {
        ArgumentNullException.ThrowIfNull(recording);
        using var stream = new MemoryStream();
        WriteMagic(stream);
        WriteUInt16(stream, FormatVersion);
        WriteUInt16(stream, MinorVersion);

        // 元数据不压缩：前缀读它时不必先进解压上下文，这是不变量而非优化
        WriteChunk(stream, ReplayChunkType.Meta, MessagePackSerializer.Serialize(recording.Meta), ReplayChunkCodec.Raw);
        WriteChunk(stream, ReplayChunkType.UnitInit, MessagePackSerializer.Serialize(recording.Units), ReplayChunkCodec.Raw);
        // 输入轨道是体积全部来源，逐块压缩
        WriteChunk(stream, ReplayChunkType.MoveTrack, MessagePackSerializer.Serialize(recording.MoveTracks), ReplayChunkCodec.Deflate);
        WriteChunk(stream, ReplayChunkType.Cast, MessagePackSerializer.Serialize(recording.Casts), ReplayChunkCodec.Deflate);
        WriteChunk(stream, ReplayChunkType.Focus, MessagePackSerializer.Serialize(recording.Focuses), ReplayChunkCodec.Deflate);

        WriteMagic(stream);
        return stream.ToArray();
    }

    /// <summary>
    /// 解码整档：容器头尾、逐块长度与校验和全部通过才算成功，未知块跳过。
    /// 不校验内容修订号与逻辑修订号，那是重放端的门控。
    /// </summary>
    public static ReplayDecodeResult Decode(ReadOnlyMemory<byte> data) {
        var (Status, _, Reason) = ReadHeader(data);
        if (Status != ReplayArchiveStatus.Ok)
            return new ReplayDecodeResult(Status, Reason: Reason);

        ReplayMeta? meta = null;
        var units = new List<ReplayUnitInit>();
        var moves = new List<ReplayMoveTrack>();
        var casts = new List<ReplayCastEntry>();
        var focuses = new List<ReplayFocusEntry>();
        // 写侧每类型恒一块：同类重复块是写侧从不产出的形状，累加只会造出同 ID 双单位或整条轨道消失。
        // 未知类型不参与该断言——多块语义由未来的块自行定义，此处拦不得。
        bool unitsSeen = false, movesSeen = false, castsSeen = false, focusesSeen = false;
        int offset = HeaderBytes;

        while (data.Length - offset > TrailerBytes) {
            if (!TryReadChunkHeader(data.Span[offset..], out var type, out var codec,
                    out int storedLen, out int rawLen, out uint crc))
                return Bad("块头不完整或长度越界");
            if (data.Length - offset - ChunkHeaderBytes < storedLen)
                return Bad("块体不完整");

            var stored = data.Slice(offset + ChunkHeaderBytes, storedLen);
            offset += ChunkHeaderBytes + storedLen;
            if (ReplayCrc32.Hash(stored.Span) != crc)
                return Bad($"块 {type} 校验和不符");

            var payload = ExpandPayload(codec, rawLen, stored, out string? expandError);
            if (payload is null)
                return Bad(expandError);

            switch (type) {
                case ReplayChunkType.Meta:
                    if (meta is not null)
                        return Bad("元数据块重复");
                    if (!TryDeserialize(payload.Value, out ReplayMeta? parsedMeta))
                        return Bad("元数据块解码失败");
                    meta = parsedMeta;
                    break;
                case ReplayChunkType.UnitInit:
                    if (unitsSeen)
                        return Bad("单位初始态块重复");
                    unitsSeen = true;
                    if (!TryDeserialize(payload.Value, out ReplayUnitInit[]? parsed))
                        return Bad("单位初始态解码失败");
                    units.AddRange(parsed);
                    break;
                case ReplayChunkType.MoveTrack:
                    if (movesSeen)
                        return Bad("移动轨道块重复");
                    movesSeen = true;
                    if (!TryDeserialize(payload.Value, out ReplayMoveTrack[]? tracks))
                        return Bad("移动轨道解码失败");
                    moves.AddRange(tracks);
                    break;
                case ReplayChunkType.Cast:
                    if (castsSeen)
                        return Bad("施法条目块重复");
                    castsSeen = true;
                    if (!TryDeserialize(payload.Value, out ReplayCastEntry[]? castEntries))
                        return Bad("施法条目解码失败");
                    casts.AddRange(castEntries);
                    break;
                case ReplayChunkType.Focus:
                    if (focusesSeen)
                        return Bad("聚焦条目块重复");
                    focusesSeen = true;
                    if (!TryDeserialize(payload.Value, out ReplayFocusEntry[]? focusEntries))
                        return Bad("聚焦条目解码失败");
                    focuses.AddRange(focusEntries);
                    break;
                default:
                    // 未知块：读侧不认识其语义，跳过后其余部分仍可重放，这是小版本演进的立足点
                    break;
            }
        }

        if (data.Length - offset != TrailerBytes || !data.Span.Slice(offset, TrailerBytes).SequenceEqual(Magic))
            return Bad("缺容器尾部标识，归档不完整");
        if (meta is null)
            return Bad("缺元数据块");

        return new ReplayDecodeResult(ReplayArchiveStatus.Ok,
            new ReplayRecording(meta, units, moves, casts, focuses));

        static ReplayDecodeResult Bad(string? reason) => new(ReplayArchiveStatus.Malformed, Reason: reason);
    }

    /// <summary>
    /// 只读元数据块：前缀够长就能拿到摘要，不触碰输入轨道，本地缓存枚举因此不必整档解码。
    /// 刻意不门控内容与逻辑修订号——列表只负责展示，能否重放由 <see cref="Decode"/> 与重放端裁决。
    /// </summary>
    public static ReplayMetaResult TryReadMeta(ReadOnlyMemory<byte> prefix) {
        var (Status, RequiredBytes, Reason) = ReadHeader(prefix);
        if (Status != ReplayArchiveStatus.Ok)
            return new ReplayMetaResult(Status, RequiredBytes: RequiredBytes, Reason: Reason);
        if (prefix.Length < MetaProbeBytes)
            return new ReplayMetaResult(ReplayArchiveStatus.NeedMoreData, RequiredBytes: MetaProbeBytes);
        if (!TryReadChunkHeader(prefix.Span[HeaderBytes..], out var type, out var codec,
                out int storedLen, out int rawLen, out uint crc))
            return new ReplayMetaResult(ReplayArchiveStatus.Malformed, Reason: "元数据块头不完整");
        // 元数据写侧固定原样存储，前缀读依赖的正是这条不变量，压缩即破格
        if (type != ReplayChunkType.Meta || codec != ReplayChunkCodec.Raw || rawLen != storedLen)
            return new ReplayMetaResult(ReplayArchiveStatus.Malformed, Reason: "首块不是原样存储的元数据块");

        int required = MetaProbeBytes + storedLen;
        if (prefix.Length < required)
            return new ReplayMetaResult(ReplayArchiveStatus.NeedMoreData, RequiredBytes: required);

        var stored = prefix.Span.Slice(MetaProbeBytes, storedLen);
        if (ReplayCrc32.Hash(stored) != crc)
            return new ReplayMetaResult(ReplayArchiveStatus.Malformed, Reason: "元数据块校验和不符");
        if (!TryDeserialize(prefix.Slice(MetaProbeBytes, storedLen), out ReplayMeta? meta))
            return new ReplayMetaResult(ReplayArchiveStatus.Malformed, Reason: "元数据块解码失败");

        return new ReplayMetaResult(ReplayArchiveStatus.Ok, meta);
    }

    // 容器头三道关：长度、魔数、格式版本。RequiredBytes 只在前缀不足时有意义
    private static (ReplayArchiveStatus Status, int RequiredBytes, string? Reason) ReadHeader(ReadOnlyMemory<byte> data) {
        if (data.Length < HeaderBytes)
            return (ReplayArchiveStatus.NeedMoreData, HeaderBytes, null);
        if (!data.Span[..Magic.Length].SequenceEqual(Magic))
            return (ReplayArchiveStatus.Malformed, 0, "魔数不符，不是回放归档");
        int formatVersion = BinaryPrimitives.ReadUInt16LittleEndian(data.Span.Slice(Magic.Length, 2));
        if (formatVersion != FormatVersion)
            return (ReplayArchiveStatus.UnsupportedVersion, 0,
                $"回放格式版本 {formatVersion} 不由本机读取，当前 {FormatVersion}");
        return (ReplayArchiveStatus.Ok, 0, null);
    }

    // 块头解析，入参是自块首起的可读区间；块体长度由调用方校验
    private static bool TryReadChunkHeader(ReadOnlySpan<byte> span, out ReplayChunkType type, out ReplayChunkCodec codec,
        out int storedLen, out int rawLen, out uint crc) {
        type = default;
        codec = default;
        storedLen = rawLen = 0;
        crc = 0;
        if (span.Length < ChunkHeaderBytes)
            return false;

        ushort rawType = BinaryPrimitives.ReadUInt16LittleEndian(span[..2]);
        byte rawCodec = span[2];
        storedLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(3, 4));
        rawLen = (int)BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(7, 4));
        crc = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(11, 4));
        if (rawCodec > (byte)ReplayChunkCodec.Deflate || storedLen < 0 || rawLen <= 0 || rawLen > MaxChunkRawBytes)
            return false;

        type = (ReplayChunkType)rawType;
        codec = (ReplayChunkCodec)rawCodec;
        return true;
    }

    // 按块头声明还原负载：还原后的长度必须与声明一致，多解少解都是损坏
    private static ReadOnlyMemory<byte>? ExpandPayload(ReplayChunkCodec codec, int rawLen,
        ReadOnlyMemory<byte> stored, out string? error) {
        error = null;
        if (codec == ReplayChunkCodec.Raw) {
            if (stored.Length != rawLen) {
                error = "原样块的存储长度与声明不符";
                return null;
            }

            return stored;
        }

        var output = new byte[rawLen];
        try {
            using var source = new MemoryStream(stored.ToArray());
            using var deflate = new DeflateStream(source, CompressionMode.Decompress);
            if (deflate.ReadAtLeast(output, rawLen, throwOnEndOfStream: false) != rawLen) {
                error = "压缩块提前耗尽";
                return null;
            }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException) {
            error = "压缩块解码失败";
            return null;
        }

        return output;
    }

    private static bool TryDeserialize<T>(ReadOnlyMemory<byte> payload,
        [NotNullWhen(true)] out T? value) {
        try {
            value = MessagePackSerializer.Deserialize<T>(payload);
            return value is not null;
        }
        catch (Exception ex) when (ex is MessagePackSerializationException or InvalidDataException) {
            value = default;
            return false;
        }
    }

    private static void WriteMagic(Stream stream) => stream.Write(Magic);

    private static void WriteChunk(Stream stream, ReplayChunkType type, byte[] payload, ReplayChunkCodec codec) {
        byte[] stored = codec == ReplayChunkCodec.Deflate ? Compress(payload) : payload;
        WriteUInt16(stream, (ushort)type);
        stream.WriteByte((byte)codec);
        WriteUInt32(stream, (uint)stored.Length);
        WriteUInt32(stream, (uint)payload.Length);
        WriteUInt32(stream, ReplayCrc32.Hash(stored));
        stream.Write(stored);
    }

    private static byte[] Compress(byte[] payload) {
        using var output = new MemoryStream(payload.Length / 2 + 256);
        using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true)) {
            deflate.Write(payload);
        }

        return output.ToArray();
    }

    private static void WriteUInt16(Stream stream, int value) {
        Span<byte> buffer = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(buffer, (ushort)value);
        stream.Write(buffer);
    }

    private static void WriteUInt32(Stream stream, uint value) {
        Span<byte> buffer = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }
}

