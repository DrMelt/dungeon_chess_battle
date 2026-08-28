using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DungeonChessBattle.Replay.Shared;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放本地缓存：目录内以 <c>{roomId}.replay</c> 存放归档字节流，文件内容与服务端归档逐字节同形。
/// 条目元数据不另存副本，从文件前缀只读记录头部得到，因此没有第二份真相，也没有旁文件配对失败。
/// 键只做文件系统安全化，不做业务校验；损坏与版本不符由读取方解码时判定。
/// </summary>
public sealed class ReplayCache {
    private const string Extension = ".replay";

    /// <summary>枚举时读取的文件前缀字节数；记录头部远小于此，装不下时整读重试一次。</summary>
    private const int HeaderProbeBytes = 8 * 1024;

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

    /// <summary>同步读取本地副本字节流；不存在或不可读时返回 false，供播放路径立即取快照。</summary>
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
    /// 枚举本地条目：逐个文件读前缀解记录头部，前缀装不下则整读重试一次。
    /// 解不出头部的副本不参与列表（半截文件或外部塞入的无关文件），留给按房间读取时判损坏重下；
    /// 房间 ID 取自头部而非文件名，文件名做过非法字符替换不可逆。
    /// </summary>
    public async Task<IReadOnlyList<ReplayRecordHeader>> ReadEntriesAsync(CancellationToken cancellationToken = default) {
        var entries = new List<ReplayRecordHeader>();
        foreach (string path in Directory.EnumerateFiles(_directory, "*" + Extension)) {
            cancellationToken.ThrowIfCancellationRequested();
            try {
                var prefix = await ReadPrefixAsync(path, cancellationToken);
                if (ReplayRecordCoder.TryReadHeader(prefix, out var header)
                    || ReplayRecordCoder.TryReadHeader(await File.ReadAllBytesAsync(path, cancellationToken), out header))
                    entries.Add(header!);
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

    // 只读前缀：头部在归档最前，为一行列表读完整场回放是浪费
    private static async Task<byte[]> ReadPrefixAsync(string path, CancellationToken cancellationToken) {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
            bufferSize: 4096, FileOptions.Asynchronous);
        var buffer = new byte[Math.Min(HeaderProbeBytes, (int)stream.Length)];
        int offset = 0;
        while (offset < buffer.Length) {
            int read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken);
            if (read == 0)
                break;
            offset += read;
        }
        return offset == buffer.Length ? buffer : buffer[..offset];
    }

    // 房间 ID 由服务端生成，此处仅兜住文件系统非法字符，避免越目录写入
    private string PathOf(string roomId) {
        foreach (char c in Path.GetInvalidFileNameChars())
            roomId = roomId.Replace(c, '_');
        return Path.Combine(_directory, roomId + Extension);
    }
}

