using LiteNetLib.Utils;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 房间端口服务器可靠消息帧的编解码唯一权威。
/// 帧布局 [0xDC 包头][消息类型][消息体]，服务端与客户端共读本类，两端不再手写帧偏移。
/// </summary>
public static class ReliableMessageFrame {
    /// <summary>帧头长度：0xDC 包头与消息类型各一字节。</summary>
    public const int HeaderLength = 2;

    /// <summary>写入帧头：0xDC 包头与可靠消息类型，随后由调用方写消息体。</summary>
    public static void WriteHeader(NetDataWriter writer) {
        writer.Put(NetworkDefaults.PacketHeader);
        writer.Put(NetworkDefaults.ReliableServerMessage);
    }

    /// <summary>识别可靠消息帧并返回消息体读取器；非可靠消息帧返回 false，body 未定义。</summary>
    public static bool TryReadBody(ReadOnlySpan<byte> data, out NetDataReader body) {
        if (data.Length < HeaderLength || data[0] != NetworkDefaults.PacketHeader
            || data[1] != NetworkDefaults.ReliableServerMessage) {
            body = null!;
            return false;
        }
        body = new NetDataReader(data[HeaderLength..].ToArray());
        return true;
    }
}
