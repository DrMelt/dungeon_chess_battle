using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteNetLib.Utils;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Entities;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 基于 LiteNetLib + LiteEntitySystem 的服务端网络管理。
/// 替代原有的 ServerNetworkManager 和 GameMessageHandler。
/// </summary>
public class EntityNetworkServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ServerEntityManager _entityManager;
    private const int DefaultPort = 9050;
    private const string ConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;

    public ServerEntityManager EntityManager => _entityManager;
    public event Action<int>? OnClientConnected;
    public event Action<int>? OnClientDisconnected;
    /// <summary>非 LES 自定义包回调。第一个 byte 已跳过 LES header。</summary>
    public event Action<NetPeer, ReadOnlySpan<byte>>? OnCustomPacket;

    public EntityNetworkServer() {
        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ServerEntityManager(
            typesMap,
            PacketHeader,
            framesPerSecond: 20,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    public void Start(int port = DefaultPort) {
        _netManager.Start(port);
        Console.WriteLine($"[EntityNetwork] Listening on port {port}");
    }

    public void Stop() {
        _netManager.Stop();
        Console.WriteLine("[EntityNetwork] Stopped");
    }

    public void PollEvents() {
        _netManager.PollEvents();
    }

    public int PeerCount => _netManager.ConnectedPeersCount;

    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey(ConnectionKey);
    }

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var player = _entityManager.AddPlayer(lesPeer);
        Console.WriteLine($"[EntityNetwork] Peer connected: {peer.Id}, PlayerId: {player?.Id}");
        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (peer.Tag is LiteNetLibNetPeer lesPeer)
            _entityManager.RemovePlayer(lesPeer);
        Console.WriteLine($"[EntityNetwork] Peer disconnected: {peer.Id}, Reason: {disconnectInfo.Reason}");
        OnClientDisconnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        Console.WriteLine($"[EntityNetwork] Error: {socketError} from {endPoint}");
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var data = reader.GetRemainingBytes();
        if (data.Length > 0 && data[0] == PacketHeader) {
            if (peer.Tag is LiteNetLibNetPeer lesPeer)
                _entityManager.Deserialize(lesPeer, data);
        }
        else {
            OnCustomPacket?.Invoke(peer, data);
        }
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }
}
