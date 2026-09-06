namespace DungeonChessBattle.Lobby.Protocol;

/// <summary>
/// SignalR Hub 端点路径常量，客户端与服务端共用，消除硬编码路径。
/// </summary>
public static class HubPaths {
    /// <summary>大厅 SignalR Hub 路径。</summary>
    public const string Lobby = "/lobby";
}