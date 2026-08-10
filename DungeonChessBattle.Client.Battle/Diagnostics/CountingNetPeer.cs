using LiteEntitySystem.Transport;

namespace DungeonChessBattle.Client.Battle.Diagnostics;

/// <summary>
/// 网络状态采集用的计数装饰器：包装 LES 传输 peer，在发送时累计出站字节与包数，
/// 使房间链路的出站流量可被网络状态统计采集。
/// 仅委托 <see cref="AbstractNetPeer"/> 的四个发送相关方法，不影响实体同步。
/// </summary>
/// <param name="inner">被包装的内部传输 peer（通常为 <see cref="LiteNetLibNetPeer"/>）。</param>
public sealed class CountingNetPeer(AbstractNetPeer inner) : AbstractNetPeer {
    private readonly AbstractNetPeer _inner = inner;

    /// <summary>累计出站字节数。</summary>
    private long _bytesOut;

    /// <summary>累计出站包数。</summary>
    private int _packetsOut;

    /// <summary>累计的出站字节数。</summary>
    public long BytesOut => _bytesOut;

    /// <summary>累计的出站包数。</summary>
    public int PacketsOut => _packetsOut;

    /// <summary>清零出站计数（每秒结算或断开/重连时调用）。</summary>
    public void ResetTraffic() {
        _bytesOut = 0;
        _packetsOut = 0;
    }

    /// <summary>发送可靠有序数据并累计出站流量。</summary>
    /// <param name="data">待发送的字节数据。</param>
    public override void SendReliableOrdered(ReadOnlySpan<byte> data) {
        _bytesOut += data.Length;
        _packetsOut++;
        _inner.SendReliableOrdered(data);
    }

    /// <summary>发送不可靠数据并累计出站流量。</summary>
    /// <param name="data">待发送的字节数据。</param>
    public override void SendUnreliable(ReadOnlySpan<byte> data) {
        _bytesOut += data.Length;
        _packetsOut++;
        _inner.SendUnreliable(data);
    }

    /// <summary>触发内部 peer 执行一次发送。</summary>
    public override void TriggerSend() => _inner.TriggerSend();

    /// <summary>获取内部 peer 的不可靠数据最大包大小。</summary>
    public override int GetMaxUnreliablePacketSize() => _inner.GetMaxUnreliablePacketSize();
}
