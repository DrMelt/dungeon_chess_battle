using System.Net;
using System.Text.Json;
using DungeonChessBattle.Replay.Protocol;
using DungeonChessBattle.Replay.Protocol.Dtos;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Replay.Client;

/// <summary>
/// 回放 HTTP 传输：带会话凭证的列表查询与字节流下载，回放出网的唯一出口。
/// 凭证与服务器根地址每次请求现取——连接可能已重登换发凭证，缓存下来就会用到旧值。
/// 失败一律以 <see cref="ReplayTransportStatus"/> 分类返回，只有用户取消向上抛异常。
/// </summary>
/// <param name="serverBase">服务器根地址提供者。</param>
/// <param name="sessionToken">会话凭证提供者；无凭证时不出网。</param>
/// <param name="logger">日志记录器，用非泛型口以共用消费方的日志类别。</param>
internal sealed class ReplayHttpTransport(Func<Uri> serverBase, Func<string?> sessionToken, ILogger logger)
    : IDisposable {
    /// <summary>请求超时，含建连与读流；用户取消不并入此超时。</summary>
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromMinutes(5);

    private const int ChunkSize = 64 * 1024;

    private readonly HttpClient _http = new() { Timeout = RequestTimeout };

    /// <summary>取服务端回放摘要列表。无凭证或凭证失效 → Unauthorized，响应不可解或传输失败 → NetworkError。</summary>
    public async Task<(ReplayTransportStatus Status, IReadOnlyList<ReplaySummaryDto> Replays)> TryListAsync(
        CancellationToken cancellationToken) {
        var (status, data) = await GetAsync(ReplayHttpRoutes.BuildListUri(serverBase()), null, cancellationToken);
        if (status != ReplayTransportStatus.Success || data is null)
            return (status, []);

        try {
            var result = JsonSerializer.Deserialize<ReplayListResult>(data, ReplayJson.Options);
            return (ReplayTransportStatus.Success, result?.Replays ?? []);
        }
        catch (JsonException ex) {
            logger.LogWarning(ex, "回放列表响应解析失败");
            return (ReplayTransportStatus.NetworkError, []);
        }
    }

    /// <summary>下载回放字节流，边读边回报进度；服务端 404 归 NotFound。</summary>
    public async Task<ReplayDownloadResult> DownloadAsync(string roomId,
        IProgress<ReplayDownloadProgress>? progress, CancellationToken cancellationToken) {
        var (status, data) = await GetAsync(ReplayHttpRoutes.BuildDownloadUri(serverBase(), roomId), progress, cancellationToken);
        return new ReplayDownloadResult(status, data);
    }

    /// <summary>释放 HttpClient。</summary>
    public void Dispose() => _http.Dispose();

    private async Task<(ReplayTransportStatus Status, byte[]? Data)> GetAsync(Uri url,
        IProgress<ReplayDownloadProgress>? progress, CancellationToken cancellationToken) {
        string? session = sessionToken();
        if (string.IsNullOrEmpty(session))
            return (ReplayTransportStatus.Unauthorized, null);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation(ReplayHttpRoutes.SessionHeader, session);
        try {
            using var response = await _http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return (ReplayTransportStatus.Unauthorized, null);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return (ReplayTransportStatus.NotFound, null);
            response.EnsureSuccessStatusCode();

            long? total = response.Content.Headers.ContentLength;
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffer = new MemoryStream(total is > 0 ? (int)Math.Min(total.Value, int.MaxValue) : ChunkSize);
            var chunk = new byte[ChunkSize];
            int read;
            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0) {
                await buffer.WriteAsync(chunk, 0, read, cancellationToken);
                progress?.Report(new ReplayDownloadProgress(buffer.Length, total));
            }
            return (ReplayTransportStatus.Success, buffer.ToArray());
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested) {
            // HttpClient 超时走此分支；用户取消时令牌已置位，继续上抛
            logger.LogWarning(ex, "回放请求超时: {Url}", url);
            return (ReplayTransportStatus.NetworkError, null);
        }
        catch (HttpRequestException ex) {
            logger.LogWarning(ex, "回放请求失败: {Url}", url);
            return (ReplayTransportStatus.NetworkError, null);
        }
    }
}
