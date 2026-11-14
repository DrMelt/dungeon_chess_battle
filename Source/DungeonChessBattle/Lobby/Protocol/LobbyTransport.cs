namespace DungeonChessBattle.Lobby.Protocol;

/// <summary>
/// 大厅传输层默认参数，客户端与服务端共读。
/// 服务端监听端口可被 --port 覆盖，此值是服务端启动默认与 UI 预填的共同来源。
/// </summary>
public static class LobbyTransport {
    /// <summary>大厅 SignalR 默认监听端口。</summary>
    public const int DefaultPort = 10170;
}
