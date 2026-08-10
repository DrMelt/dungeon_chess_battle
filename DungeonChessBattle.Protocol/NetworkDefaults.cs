namespace DungeonChessBattle.Protocol;

/// <summary>
/// 网络协议默认值（客户端与服务端共享，消除各端重复的魔法数字）。
/// </summary>
public static class NetworkDefaults {
    /// <summary>默认大厅监听端口（SignalR）。</summary>
    public const int LobbyPort = 10170;
}
