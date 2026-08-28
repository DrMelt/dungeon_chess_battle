namespace DungeonChessBattle.Replay.Protocol;

/// <summary>
/// 回放 HTTP 契约唯一来源：路由、会话凭证头与 URL 组装，服务端端点映射与客户端调用共用，消除字面量漂移。
/// 回放不借用大厅 SignalR 连接：身份由大厅登录时签发的会话凭证随请求头带出，
/// 因此回放两端与大厅的连接实现互不相识。
/// </summary>
public static class ReplayHttpRoutes {
    /// <summary>会话凭证请求头。凭证由服务端登录流程签发，与登录会话同生命周期。</summary>
    public const string SessionHeader = "X-Dcb-Session";

    /// <summary>回放端点路径前缀，服务端映射与客户端组装的共同底。</summary>
    public const string Prefix = "/replay";

    /// <summary>回放摘要列表端点路由。</summary>
    public const string List = Prefix + "/list";

    /// <summary>回放字节流下载端点路由模式，服务端映射用。</summary>
    public const string DownloadPattern = Prefix + "/{roomId}";

    /// <summary>归档字节流媒体类型。</summary>
    public const string ContentType = "application/octet-stream";

    /// <summary>按服务器根地址组装列表 URL。</summary>
    public static Uri BuildListUri(Uri serverBase) => new(serverBase, List);

    /// <summary>按服务器根地址与房间 ID 组装下载 URL；凭证走请求头，不进查询串。</summary>
    public static Uri BuildDownloadUri(Uri serverBase, string roomId)
        => new(serverBase, $"{Prefix}/{Uri.EscapeDataString(roomId)}");
}
