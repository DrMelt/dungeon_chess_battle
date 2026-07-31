using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Entities;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 单房间的 LES 实体服务器。每个房间拥有独立的 NetManager + ServerEntityManager，
/// 实现物理级别的 Entity 同步隔离。
/// 创建 Entity 时仅该房间内的客户端可见。
/// </summary>
public class RoomEntityServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ServerEntityManager _entityManager;
    private readonly ILogger<RoomEntityServer> _logger;
    private const string ConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;

    // 玩家实体跟踪（按 NetPeer.Id 索引）
    private readonly Dictionary<int, PlayerRoomEntity> _playerEntities = [];
    private readonly Dictionary<int, NetPlayer> _netPlayers = [];
    private readonly Dictionary<int, UnitController> _unitControllers = [];

    public ServerEntityManager EntityManager => _entityManager;
    public int Port { get; }
    public string RoomId { get; }
    public int PeerCount => _netManager.ConnectedPeersCount;

    public event Action<int>? OnClientConnected;
    public event Action<int>? OnClientDisconnected;

    /// <param name="port">监听端口</param>
    /// <param name="roomId">房间标识</param>
    /// <param name="logger">日志器</param>
    public RoomEntityServer(int port, string roomId, ILogger<RoomEntityServer> logger) {
        Port = port;
        RoomId = roomId;
        _logger = logger;

        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ServerEntityManager(
            typesMap,
            PacketHeader,
            framesPerSecond: 60,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    public void Start() {
        _netManager.Start(Port);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer] Room '{RoomId}' listening on port {Port}", RoomId, Port);
    }

    public void Stop() {
        _netManager.Stop();
        _logger.LogInformation("[RoomServer] Room '{RoomId}' stopped on port {Port}", RoomId, Port);
    }

    public void PollEvents() {
        _netManager.PollEvents();
    }

    // ── INetEventListener ─────────────────────────────────

    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey(ConnectionKey);
    }

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var player = _entityManager.AddPlayer(lesPeer);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Peer connected: {PeerId}, PlayerId: {PlayerId}", RoomId, peer.Id, player?.Id);

        // 追踪 NetPlayer（用于创建 UnitController 等需要玩家引用的操作）
        if (player != null)
            _netPlayers[peer.Id] = player;

        // 为房间内每个客户端创建 PlayerRoomEntity
        var playerEntity = _entityManager.AddEntity<PlayerRoomEntity>(e => {
            e.PlayerName.Value = $"Player_{peer.Id}";
            e.IsReady.Value = false;
            e.Camp.Value = 0;
        });
        if (playerEntity != null) {
            _playerEntities[peer.Id] = playerEntity;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] PlayerRoomEntity created for peer {PeerId}", RoomId, peer.Id);
        }

        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (peer.Tag is LiteNetLibNetPeer lesPeer)
            _entityManager.RemovePlayer(lesPeer);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Peer disconnected: {PeerId}, Reason: {Reason}", RoomId, peer.Id, disconnectInfo.Reason);
        OnClientDisconnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        _logger.LogError("[RoomServer:{RoomId}] Error: {SocketError} from {EndPoint}", RoomId, socketError, endPoint);
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var data = reader.GetRemainingBytes();
        if (data.Length > 0 && data[0] == PacketHeader) {
            if (peer.Tag is LiteNetLibNetPeer lesPeer)
                _entityManager.Deserialize(lesPeer, data);
        }
        // 房间端口不处理 JSON 自定义包（所有逻辑走 LES RPC）
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }
}