using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Replay.Client;
using DungeonChessBattle.Replay.Protocol.Dtos;
using DungeonChessBattle.Replay.Shared;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.Services;

/// <summary>
/// 回放浏览服务：组合 ReplayClient（服务端获取）与 ReplayCache（本地缓存）。
/// 唯一的消费侧裁决点——列表并集、版本与内容门控、下载进度与在途任务都在此归并，
/// 对外只暴露「行视图结论」与「获取/启动动作」，文案由视图层按动作语义翻译。
/// 会话失效时调用 <see cref="OnSessionInvalid"/>，由客户端连接状态机驱动。
/// 是否启动回放由表现层显式决定，本类不触发播放。
/// </summary>
/// <param name="client">服务端回放获取。</param>
/// <param name="cache">本地回放缓存。</param>
/// <param name="logger">日志记录器。</param>
public sealed class ReplayService(ReplayClient client, ReplayCache cache, ILogger<ReplayService> logger) : IDisposable {
    /// <summary>本地副本上限，超出按最后写入时间淘汰最旧。</summary>
    private const int MaxCachedReplays = 64;

    // 在途与进度是多线程共享：后台线程写进度、主线程读行视图。
    private readonly ConcurrentDictionary<string, (long BytesReceived, long? TotalBytes)> _progress = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _pending = new();

    // 列表快照：后台刷新整引用替换、主线程整引用读取，volatile 保证可见性。
    private volatile IReadOnlyList<ReplayListEntry>? _entries;

    /// <summary>录制端两项修订号与本地是否一致：内容修订号管配置与布局，逻辑修订号管结算时序。任一缺失判不一致。</summary>
    private static bool IsContentCompatible(string? dataVersion, string? logicVersion) =>
        !string.IsNullOrEmpty(dataVersion) && dataVersion == GameConfigDB.DataRevision
        && !string.IsNullOrEmpty(logicVersion) && logicVersion == BattleLogicRevision.Value;

    /// <summary>取合并后的行视图：基于静态列表快照现场构建动态可用态，进度实时；列表未刷新时返回空。</summary>
    public IReadOnlyList<ReplayRowView> GetRowViews() {
        var entries = _entries;
        if (entries is null || entries.Count == 0)
            return [];
        var views = new ReplayRowView[entries.Count];
        for (int i = 0; i < entries.Count; i++)
            views[i] = BuildRow(entries[i]);
        return views;
    }

    /// <summary>刷新合并列表（本地 ∪ 服务端），fire-and-forget，异常兜底不外抛。</summary>
    public void RefreshList() => _ = RefreshListAsync();

    private async Task RefreshListAsync() {
        try {
            _entries = await BuildMergedEntriesAsync(CancellationToken.None);
        }
        catch (Exception ex) {
            logger.LogError(ex, "回放列表刷新失败");
        }
    }

    /// <summary>合并本地条目与服务端条目，服务端覆盖同房间本地条目，按开始时间倒序。</summary>
    private async Task<IReadOnlyList<ReplayListEntry>> BuildMergedEntriesAsync(CancellationToken cancellationToken) {
        var merged = new Dictionary<string, ReplayListEntry>();
        // 本地枚举只交回格式版本可读的副本；能否重放仍由解码与内容门控裁决。
        // 两条来源都先归成同一个 DTO，字段清单就不随来源分叉，差异只剩 FromServer 一项
        foreach (var meta in await cache.ReadEntriesAsync(cancellationToken))
            merged[meta.RoomId] = ToEntry(ReplaySummaryDto.From(meta), fromServer: false);
        foreach (var dto in await client.GetServerListAsync(cancellationToken))
            merged[dto.RoomId] = ToEntry(dto, fromServer: true);

        var entries = new List<ReplayListEntry>(merged.Values);
        entries.Sort(static (a, b) => {
            int byTime = b.StartUnixTime.CompareTo(a.StartUnixTime);
            return byTime != 0 ? byTime : string.CompareOrdinal(a.RoomId, b.RoomId);
        });
        return entries;
    }

    /// <summary>发起一场回放获取；非列表内、在途或内容不兼容时忽略。结果只写缓存与进度，由下一帧视图反映。</summary>
    public void RequestFetch(string roomId) {
        if (_pending.ContainsKey(roomId))
            return;
        ReplayListEntry? entry = _entries?.FirstOrDefault(e => e.RoomId == roomId);
        if (entry is null || !IsContentCompatible(entry.DataVersion, entry.LogicVersion))
            return;
        _ = FetchAsync(roomId);
    }

    /// <summary>取本地可重放记录；副本损坏时移除文件以允许重新下载。版本不符返回 Unsupported，修订号不符返回 Incompatible。</summary>
    public ReplayPlayableResult TryGetPlayable(string roomId) {
        if (!cache.TryRead(roomId, out var data))
            return new ReplayPlayableResult(ReplayGateStatus.NotCached);

        var result = DecodeGate(roomId, data);
        if (result.Status == ReplayGateStatus.Corrupted) {
            cache.Invalidate(roomId);
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("本地回放副本损坏，已移除，可重新下载: {RoomId}", roomId);
        }
        return result;
    }

    /// <summary>取消全部在途获取并清空过程状态。</summary>
    public void CancelAll() {
        foreach (var (roomId, cts) in _pending) {
            cts.Cancel();
            if (_pending.TryRemove(roomId, out _))
                cts.Dispose();
        }
        _progress.Clear();
    }

    /// <summary>会话失效（登出/断线）：旧凭证已作废，取消全部在途并清空过程状态。</summary>
    public void OnSessionInvalid() => CancelAll();

    /// <summary>按在途/版本兼容/本地缓存裁决一行的动作语义与可用态。</summary>
    private ReplayRowView BuildRow(ReplayListEntry entry) {
        if (_pending.ContainsKey(entry.RoomId))
            return With(entry, ReplayBrowseAction.Downloading, playEnabled: false, DownloadPercent(entry.RoomId));
        if (!IsContentCompatible(entry.DataVersion, entry.LogicVersion))
            return With(entry, ReplayBrowseAction.Blocked, playEnabled: false, downloadPercent: null);
        return cache.Contains(entry.RoomId)
            ? With(entry, ReplayBrowseAction.Play, playEnabled: true, downloadPercent: null)
            : With(entry, ReplayBrowseAction.Download, playEnabled: false, downloadPercent: null);
    }

    private static ReplayRowView With(ReplayListEntry e, ReplayBrowseAction action, bool playEnabled, int? downloadPercent)
        => new(e.RoomId, e.DungeonKey, e.StartUnixTime, e.TickRate, e.DurationTicks, e.PlayerNames, e.FromServer,
            action, playEnabled, downloadPercent);

    private int? DownloadPercent(string roomId) {
        if (!_progress.TryGetValue(roomId, out var p) || p.TotalBytes is not { } total || total <= 0)
            return null;
        return (int)(p.BytesReceived * 100 / total);
    }

    private async Task FetchAsync(string roomId) {
        var cts = new CancellationTokenSource();
        if (!_pending.TryAdd(roomId, cts))
            return;
        try {
            ReplayPlayableResult result = await ResolveAsync(roomId, cts.Token);
            if (!result.IsReady && logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("回放获取失败: {RoomId}，{Status} {Reason}",
                    roomId, result.Status, result.Reason ?? "unknown");
        }
        catch (OperationCanceledException) {
            // 已被 CancelAll 或消费方取消，结果不必上抛
        }
        catch (Exception ex) {
            logger.LogError(ex, "回放获取异常: {RoomId}", roomId);
        }
        finally {
            if (_pending.TryGetValue(roomId, out var current) && ReferenceEquals(current, cts))
                _pending.TryRemove(roomId, out _);
            cts.Dispose();
        }
    }

    /// <summary>取消在途并释放底层客户端。</summary>
    public void Dispose() {
        CancelAll();
        client.Dispose();
    }

    private async Task<ReplayPlayableResult> ResolveAsync(string roomId, CancellationToken cancellationToken) {
        var (found, cached) = await cache.TryReadAsync(roomId, cancellationToken);
        if (found) {
            var cachedResult = DecodeGate(roomId, cached);
            // 副本损坏或格式版本落后于本机读取端：重下一次就可能拿到可用归档，不就此止步
            if (cachedResult.Status is not (ReplayGateStatus.Corrupted or ReplayGateStatus.Unsupported))
                return cachedResult;
            cache.Invalidate(roomId);
            if (logger.IsEnabled(LogLevel.Information))
                logger.LogInformation("本地回放副本损坏，重新下载: {RoomId}", roomId);
        }

        var progress = new Progress<ReplayDownloadProgress>(p => _progress[roomId] = (p.BytesReceived, p.TotalBytes));
        var (status, data) = await client.DownloadArchiveAsync(roomId, progress, cancellationToken);
        if (status != ReplayTransportStatus.Success || data is null)
            return new ReplayPlayableResult(ReplayGateStatus.NotCached, Reason: FailureText(status, roomId));

        var result = DecodeGate(roomId, data);
        if (result.IsReady) {
            await cache.WriteAsync(roomId, data, cancellationToken);
            TrimCache();
        }
        return result;
    }

    /// <summary>解码并门控：容器不合规范判损坏，格式版本不认判不支持，修订号不符判不兼容。</summary>
    private ReplayPlayableResult DecodeGate(string roomId, byte[] data) {
        var decoded = ReplayArchive.Decode(data);
        if (decoded.Status == ReplayArchiveStatus.UnsupportedVersion)
            return new ReplayPlayableResult(ReplayGateStatus.Unsupported, Reason: decoded.Reason);
        if (decoded.Status != ReplayArchiveStatus.Ok || decoded.Recording is not { } recording) {
            if (logger.IsEnabled(LogLevel.Warning))
                logger.LogWarning("回放数据解码失败: {RoomId}，{Reason}", roomId, decoded.Reason ?? "unknown");
            return new ReplayPlayableResult(ReplayGateStatus.Corrupted, Reason: "回放数据无法解码。");
        }

        if (!IsContentCompatible(recording.Meta.DataVersion, recording.Meta.LogicVersion))
            return new ReplayPlayableResult(ReplayGateStatus.Incompatible, Reason:
                $"回放由内容 {recording.Meta.DataVersion}/逻辑 {recording.Meta.LogicVersion} 录制，" +
                $"本地为 {GameConfigDB.DataRevision}/{BattleLogicRevision.Value}。");

        return new ReplayPlayableResult(ReplayGateStatus.Ready, recording);
    }

    private void TrimCache() {
        int removed = cache.TrimTo(MaxCachedReplays);
        if (removed > 0 && logger.IsEnabled(LogLevel.Information))
            logger.LogInformation("本地回放缓存超出 {Max} 场，淘汰最旧副本 {Removed} 个", MaxCachedReplays, removed);
    }

    /// <summary>摘要条目 → 列表条目：唯一的来源无关出口，玩家名之外的字段一律直传。</summary>
    private static ReplayListEntry ToEntry(ReplaySummaryDto dto, bool fromServer) => new(
        dto.RoomId,
        dto.DungeonKey,
        dto.StartUnixTime,
        dto.TickRate,
        dto.DurationTicks,
        dto.DataVersion,
        dto.LogicVersion,
        [.. dto.Players.Select(static player => player.PlayerName)],
        fromServer);

    private static string FailureText(ReplayTransportStatus status, string roomId) => status switch {
        ReplayTransportStatus.Unauthorized => "未取得会话凭证或凭证已失效，需重新登录大厅。",
        ReplayTransportStatus.NotFound => $"服务端不存在回放 {roomId}，或你不在参与者内。",
        _ => $"回放 {roomId} 下载失败。",
    };
}

