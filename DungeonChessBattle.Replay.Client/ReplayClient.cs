using DungeonChessBattle.Replay.Protocol.Dtos;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Replay.Client;

/// <summary>
/// 回放获取客户端：只向服务端取回放事实——服务端归档摘要列表与单场归档字节流。
/// 不解码、不门控、不缓存、不并集，这些消费侧决策归 Game 层的回放浏览服务。
/// 出网全经 <see cref="ReplayHttpTransport"/>，本类不碰 HTTP 与凭证细节，也不绑定任何场景节点。
/// </summary>
/// <param name="serverBase">服务器根地址提供者，交给传输层每次现取。</param>
/// <param name="sessionToken">会话凭证提供者，登录后才有值；无凭证时服务端侧一律降级。</param>
/// <param name="logger">日志记录器。</param>
public sealed class ReplayClient(Func<Uri> serverBase, Func<string?> sessionToken, ILogger<ReplayClient> logger) : IDisposable {
    private readonly ReplayHttpTransport _transport = new(serverBase, sessionToken, logger);

    /// <summary>取服务端归档的摘要列表；服务端不可用降级为空列表，不抛，由消费方决定呈现。</summary>
    public async Task<IReadOnlyList<ReplaySummaryDto>> GetServerListAsync(CancellationToken cancellationToken = default) {
        var (status, replays) = await _transport.TryListAsync(cancellationToken);
        if (status != ReplayTransportStatus.Success && logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("回放列表服务端侧不可用，仅呈现本地条目: {Status}", status);
        return status == ReplayTransportStatus.Success ? replays : [];
    }

    /// <summary>
    /// 下载单个回放归档字节流，边收边回报进度。服务端 404/401 以状态返回，不解码。
    /// 返回服务端归档原字节，后续解码与门控由消费方执行。
    /// </summary>
    public Task<ReplayDownloadResult> DownloadArchiveAsync(string roomId,
        IProgress<ReplayDownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        => _transport.DownloadAsync(roomId, progress, cancellationToken);

    /// <summary>释放传输层。</summary>
    public void Dispose() => _transport.Dispose();
}

