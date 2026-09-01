using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放本地缓存：目录内以 <c>{roomId}.replay</c> 存放归档字节流，文件内容与服务端归档逐字节同形。
/// 条目元数据不另存副本，按容器块头精确读元数据块即得，因此没有第二份真相，也没有旁文件配对失败。
/// 键只做文件系统安全化，不做业务校验；版本、损坏与内容一致性由读取方判定。
/// </summary>
public sealed class ReplayCache {
    private const string Extension = ".replay";

    /// <summary>元数据块长度上限，超出即不是本格式能产出的归档，跳过该副本。</summary>
    private const int MaxMetaBytes = 64 * 1024;

    private readonly string _directory;

    /// <param name="directory">缓存目录绝对路径，不存在时创建。</param>
    public ReplayCache(string directory) {
        _directory = directory;
        Directory.CreateDirectory(directory);
    }

    /// <summary>是否存在该房间的本地副本。</summary>
    public bool Contains(string roomId) => File.Exists(PathOf(roomId));

    /// <summary>读取本地副本字节流；不存在时返回 false。</summary>
    public async Task<(bool Found, byte[] Data)> TryReadAsync(string roomId, CancellationToken cancellationToken = default) {
        string path = PathOf(roomId);
        if (!File.Exists(path))
            return (false, []);
        try {
            return (true, await File.ReadAllBytesAsync(path, cancellationToken));
        }
        catch (IOException) {
            // 副本不可读按未命中处理，随后由下载覆盖重建
            return (false, []);
        }
    }

    /// <summary>同步读取本地副本字节流；不存在或不可读时返回 false，供播放路径立即解码。</summary>
    public bool TryRead(string roomId, out byte[] data) {
        string path = PathOf(roomId);
        if (!File.Exists(path)) {
            data = [];
            return false;
        }
        try {
            data = File.ReadAllBytes(path);
            return true;
        }
        catch (IOException) {
            data = [];
            return false;
        }
    }

    /// <summary>写入本地副本，覆盖既有文件。</summary>
    public async Task WriteAsync(string roomId, byte[] data, CancellationToken cancellationToken = default) {
        try {
            await File.WriteAllBytesAsync(PathOf(roomId), data, cancellationToken);
        }
        catch (IOException) {
            // 缓存写失败不影响本次回放，下次重新下载
        }
    }

    /// <summary>丢弃本地副本。</summary>
    public void Invalidate(string roomId) {
        try {
            File.Delete(PathOf(roomId));
        }
        catch (IOException) {
            // 已不可操作，忽略
        }
    }

    /// <summary>
    /// 枚举本地条目：按块头声明的精确长度只读元数据块，块头本身要先一次前缀读才拿得到，故至多两轮。
    /// 版本不符与读不出的副本不进列表（旧格式、半截文件、外部塞入的无关文件），
    /// 留给按房间读取时判损坏重下；房间 ID 取自元数据而非文件名，文件名做过非法字符替换不可逆。
    /// </summary>
    public async Task<IReadOnlyList<ReplayMeta>> ReadEntriesAsync(CancellationToken cancellationToken = default) {
        var entries = new List<ReplayMeta>();
        foreach (string path in Directory.EnumerateFiles(_directory, "*" + Extension)) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                if (await ReadMetaAsync(path, cancellationToken) is { } meta)
                    entries.Add(meta);
            }
            catch (IOException) {
                // 这个副本此刻读不动，跳过，不影响其余条目
            }
        }

        return entries;
    }

    /// <summary>按最后写入时间保留最近 maxCount 个副本，更旧的删除；返回删除数。</summary>
    public int TrimTo(int maxCount) {
        var files = new DirectoryInfo(_directory).GetFiles("*" + Extension);
        if (files.Length <= maxCount)
            return 0;

        Array.Sort(files, static (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
        int removed = 0;
        for (int i = 0; i < files.Length - maxCount; i++) {
            try {
                files[i].Delete();
                removed++;
            }
            catch (IOException) {
                // 删不掉留给下一轮，不阻断本次回放
            }
        }
        return removed;
    }

    // 元数据块读两轮：第一轮读够容器头与块头，第二轮补齐块体。
    // TryReadMeta 认的是自归档第 0 字节起的前缀，故两轮读进同一个前缀缓冲，续读只填尾部。
    private static async Task<ReplayMeta?> ReadMetaAsync(string path, CancellationToken cancellationToken) {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 4096, FileOptions.Asynchronous);
        var head = new byte[ReplayArchive.MetaProbeBytes];
        await ReadIntoAsync(stream, head, cancellationToken);
        var probe = ReplayArchive.TryReadMeta(head);
        if (probe.Status != ReplayArchiveStatus.NeedMoreData || probe.RequiredBytes > MaxMetaBytes)
            return null;

        var prefix = new byte[probe.RequiredBytes];
        head.CopyTo(prefix, 0);
        await ReadIntoAsync(stream, prefix.AsMemory(ReplayArchive.MetaProbeBytes), cancellationToken);
        var result = ReplayArchive.TryReadMeta(prefix);
        return result.Status == ReplayArchiveStatus.Ok ? result.Meta : null;
    }

    // 读满整个缓冲；文件到不了就留零尾，由调用方的魔数与校验和判它不合规范
    private static async Task ReadIntoAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken) {
        int offset = 0;
        while (offset < buffer.Length) {
            int read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0)
                break;
            offset += read;
        }
    }

    // 房间 ID 由服务端生成，此处仅兜住文件系统非法字符，避免越目录写入
    private string PathOf(string roomId) {
        foreach (char c in Path.GetInvalidFileNameChars())
            roomId = roomId.Replace(c, '_');
        return Path.Combine(_directory, roomId + Extension);
    }
}

