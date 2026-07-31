using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Entities;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 基于 LiteNetLib + LiteEntitySystem 的服务端网络管理。
/// 替代原有的 ServerNetworkManager 和 GameMessageHandler。
/// </summary>
public class EntityNetworkServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ServerEntityManager _entityManager;
    private readonly ILogger<EntityNetworkServer> _logger;
    private const int DefaultPort = 9050;
    private const string ConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;

    public ServerEntityManager EntityManager => _entityManager;
    public event Action<int>? OnClientConnected;
    public event Action<int>? OnClientDisconnected;
    /// <summary>非 LES 自定义包回调。第一个 byte 已跳过 LES header。</summary>
    public event Action<NetPeer, ReadOnlySpan<byte>>? OnCustomPacket;

    public EntityNetworkServer(ILogger<EntityNetworkServer> logger) {
        _logger = logger;
        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ServerEntityManager(
            typesMap,
            PacketHeader,
            framesPerSecond: 60,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    public void Start(int port = DefaultPort) {
        _netManager.Start(port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[EntityNetwork] Listening on port {Port}", port);
    }

    public void Stop() {
        _netManager.Stop();
        _logger.LogInformation("[EntityNetwork] Stopped");
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
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[EntityNetwork] Peer connected: {PeerId}, PlayerId: {PlayerId}", peer.Id, player?.Id);
        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (peer.Tag is LiteNetLibNetPeer lesPeer)
            _entityManager.RemovePlayer(lesPeer);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[EntityNetwork] Peer disconnected: {PeerId}, Reason: {Reason}", peer.Id, disconnectInfo.Reason);
        OnClientDisconnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        _logger.LogError("[EntityNetwork] Error: {SocketError} from {EndPoint}", socketError, endPoint);
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
