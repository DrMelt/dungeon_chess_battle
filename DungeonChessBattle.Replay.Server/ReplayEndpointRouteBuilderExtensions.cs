using DungeonChessBattle.Replay.Protocol;
using DungeonChessBattle.Server.Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace DungeonChessBattle.Replay.Server;

/// <summary>
/// 回放 HTTP 端点映射：列表与字节流下载。宿主只负责调用本方法，路由与鉴权属回放服务端。
/// 身份只认请求头里的会话凭证，经解析端口换成玩家记录主键；本库不认识连接、登录与凭证的签发方式。
/// </summary>
public static class ReplayEndpointRouteBuilderExtensions {
    /// <summary>
    /// 映射回放端点。缺凭证或凭证解析不到玩家返回 401；
    /// 下载时非参与者、房间 ID 非法或归档不存在一律 404，不暴露回放存在性。
    /// </summary>
    public static IEndpointRouteBuilder MapReplayEndpoints(this IEndpointRouteBuilder endpoints) {
        // 端点按请求解析服务，装配缺失本会在首次请求才暴露；映射期先解一次，把配置错误留在启动日志里
        _ = endpoints.ServiceProvider.GetRequiredService<ReplayServer>();

        endpoints.MapGet(ReplayHttpRoutes.List, (HttpRequest request, IPlayerIdentityResolver identity,
            ReplayServer replay) => {
                string? recordId = ResolveRecord(request, identity);
                if (recordId is null)
                    return Results.Unauthorized();
                return Results.Json(replay.GetReplays(recordId), ReplayJson.Options);
            });

        endpoints.MapGet(ReplayHttpRoutes.DownloadPattern, (string roomId, HttpRequest request,
            IPlayerIdentityResolver identity, ReplayServer replay) => {
                string? recordId = ResolveRecord(request, identity);
                if (recordId is null)
                    return Results.Unauthorized();
                if (!replay.TryGetArchive(recordId, roomId, out byte[] data))
                    return Results.NotFound();
                return Results.File(data, ReplayHttpRoutes.ContentType, $"{roomId}.replay");
            });

        return endpoints;
    }

    // 凭证只走请求头，不进 URL：查询串会落进访问日志与浏览器历史
    private static string? ResolveRecord(HttpRequest request, IPlayerIdentityResolver identity) {
        string? session = request.Headers[ReplayHttpRoutes.SessionHeader];
        return string.IsNullOrEmpty(session) ? null : identity.ResolveRecordId(session);
    }
}
