using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 大厅专用网络服务器。处理 create_room / join_room 等 JSON 消息，
/// 不管理任何 LES Entity（房间内的 Entity 由 RoomEntityServer 各自托管）。
/// 客户端加入房间后会收到端口号，然后断开大厅连接、切入房间端口。
/// </summary>
public class LobbyNetworkServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ILogger<LobbyNetworkServer> _logger;
    private const int DefaultPort = 10170;
    private const string ConnectionKey = "DungeonChessBattle";

    public event Action<NetPeer, ReadOnlySpan<byte>>? OnCustomPacket;
    public event Action<int>? OnClientConnected;
    public event Action<int>? OnClientDisconnected;

    public LobbyNetworkServer(ILogger<LobbyNetworkServer> logger) {
        _logger = logger;
        _netManager = new NetManager(this);
    }

    public void Start(int port = DefaultPort) {
        _netManager.Start(port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Listening on port {Port}", port);
    }

    public void Stop() {
        _netManager.Stop();
        _logger.LogInformation("[Lobby] Stopped");
    }

    public void PollEvents() {
        _netManager.PollEvents();
    }

    public int PeerCount => _netManager.ConnectedPeersCount;

    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey(ConnectionKey);
    }

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Peer connected: {PeerId}", peer.Id);
        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Peer disconnected: {PeerId}, Reason: {Reason}", peer.Id, disconnectInfo.Reason);
        OnClientDisconnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        _logger.LogError("[Lobby] Error: {SocketError} from {EndPoint}", socketError, endPoint);
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var data = reader.GetRemainingBytes();
        // 大厅端口不处理 LES 包（0xDC），所有数据走自定义包
        OnCustomPacket?.Invoke(peer, data);
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }
}