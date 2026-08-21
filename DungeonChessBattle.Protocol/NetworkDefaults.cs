namespace DungeonChessBattle.Protocol;

/// <summary>
/// 网络协议默认值，客户端与服务端共享，消除各端重复的魔法数字。
/// </summary>
public static class NetworkDefaults {
    /// <summary>默认大厅监听端口，SignalR。</summary>
    public const int LobbyPort = 10170;

    /// <summary>房间端口 LES 二进制协议包头，客户端与服务端共读。</summary>
    public const byte PacketHeader = 0xDC;

    /// <summary>房间端口服务器可靠消息类型，位于 0xDC 包头后第二字节。</summary>
    public const byte ReliableServerMessage = 0x10;

    /// <summary>房间端口默认连接密钥；服务端配置服务器密码时以密码替换。</summary>
    public const string ConnectionKey = "DungeonChessBattle";
}
