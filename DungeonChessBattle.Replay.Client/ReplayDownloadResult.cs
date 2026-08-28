namespace DungeonChessBattle.Replay.Client;

/// <summary>
/// 回放传输结果状态。Replay.Client 只承担从服务端取事实，状态仅覆盖传输成败，
/// 解码与内容版本门控是消费侧决策，归 Game 层回放浏览服务。
/// </summary>
public enum ReplayTransportStatus {
    /// <summary>传输成功，取得服务端返回内容。</summary>
    Success,

    /// <summary>未取得会话凭证或服务端不认：未连接、未登录、凭证已随连接作废。</summary>
    Unauthorized,

    /// <summary>服务端无此回放，或你不在参与者内；两者同回 404，不暴露回放存在性。</summary>
    NotFound,

    /// <summary>网络传输失败或响应不可解。</summary>
    NetworkError,
}

/// <summary>
/// 回放下载结果：只反映传输成败与服务端返回的原始字节，不解释内容。
/// 解码与内容版本门控是消费侧决策，归 Game 层回放浏览服务。
/// </summary>
public sealed record ReplayDownloadResult(ReplayTransportStatus Status, byte[]? Data);

/// <summary>下载进度：已收字节与总字节（服务端未给 Content-Length 时为 null）。</summary>
public readonly record struct ReplayDownloadProgress(long BytesReceived, long? TotalBytes);
