using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 大厅专用网络服务器。处理 create_room / join_room 等 JSON 消息，
/// 不管理任何 LES Entity（房间内的 Entity 由 BattleRoomServer 各自托管）。
/// 客户端加入房间后会收到端口号，然后断开大厅连接、切入房间端口。
/// 支持可选的服务器密码验证。
/// </summary>
public class LobbyNetworkServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ILogger<LobbyNetworkServer> _logger;
    private const int DefaultPort = 10170;
    private const string DefaultConnectionKey = "DungeonChessBattle";
    private readonly string? _serverPassword;

    /// <summary>自定义数据包接收事件。参数：来源 peer、原始字节数据。</summary>
    public event Action<NetPeer, ReadOnlySpan<byte>>? OnCustomPacket;
    /// <summary>客户端连接事件。参数：peer ID。</summary>
    public event Action<int>? OnClientConnected;
    /// <summary>客户端断开事件。参数：peer ID。</summary>
    public event Action<int>? OnClientDisconnected;

    /// <summary>
    /// 初始化大厅网络服务器。
    /// </summary>
    /// <param name="logger">日志记录器。</param>
    /// <param name="serverPassword">可选的服务器密码。为 null 或空时使用默认连接密钥（开发模式）。</param>
    public LobbyNetworkServer(ILogger<LobbyNetworkServer> logger, string? serverPassword = null) {
        _logger = logger;
        _serverPassword = string.IsNullOrEmpty(serverPassword) ? null : serverPassword;
        _netManager = new NetManager(this);
    }

    /// <summary>
    /// 启动网络监听。
    /// </summary>
    /// <param name="port">监听端口，默认使用 <see cref="DefaultPort"/>。</param>
    public void Start(int port = DefaultPort) {
        _netManager.Start(port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Listening on port {Port}, Password={HasPassword}", port, _serverPassword != null);
    }

    /// <summary>
    /// 停止网络监听并释放资源。
    /// </summary>
    public void Stop() {
        _netManager.Stop();
        _logger.LogInformation("[Lobby] Stopped");
    }

    /// <summary>
    /// 轮询处理网络事件，应由主循环每帧调用。
    /// </summary>
    public void PollEvents() {
        _netManager.PollEvents();
    }

    /// <summary>当前已连接的客户端数量。</summary>
    public int PeerCount => _netManager.ConnectedPeersCount;

    /// <summary>获取当前生效的连接密钥。</summary>
    public string EffectiveConnectionKey => _serverPassword ?? DefaultConnectionKey;

    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        // 使用服务器密码验证连接。如果未设置密码，使用默认连接密钥。
        request.AcceptIfKey(EffectiveConnectionKey);
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
