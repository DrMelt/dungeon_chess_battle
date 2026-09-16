namespace DungeonChessBattle.Battle.Entities;

/// <summary>
/// 房间端口二进制协议常量。
/// </summary>
public static class BattleRoomProtocol {
    /// <summary>房间端口包头，同时作为 LES EntityManager 的 headerByte。</summary>
    public const byte PacketHeader = 0xDC;

    /// <summary>可靠消息帧第二字节，标识该帧为服务器发往客户端的可靠消息。</summary>
    public const byte ReliableServerMessage = 0x10;
}
