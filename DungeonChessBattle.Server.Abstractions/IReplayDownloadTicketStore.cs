namespace DungeonChessBattle.Server.Abstractions;

/// <summary>
/// 回放下载一次性凭证契约：Hub 参与者校验通过后签发，HTTP 下载端点验证消费。
/// 凭证绑定房间、短时有效、取后即失效；回放字节流不经 SignalR 通道，经 HTTP 端点流式获取。
/// </summary>
public interface IReplayDownloadTicketStore {
    /// <summary>为指定房间签发一次性下载凭证，返回凭证串。</summary>
    string Issue(string roomId);

    /// <summary>验证并消费一次性凭证，返回其绑定的房间 ID；无效、过期或已使用返回 false。</summary>
    bool TryConsume(string ticket, out string roomId);
}
