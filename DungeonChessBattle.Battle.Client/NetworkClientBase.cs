using System.Net;
using System.Net.Sockets;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Client.Shared;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 客户端网络基础设施抽象基类。
/// 提供 NetManager、Connect/Disconnect/Reconnect/Update、INetEventListener 骨架。
/// 子类重写 OnNetworkReceive、OnPeerConnected、OnPeerDisconnected 实现具体协议。
/// </summary>
public abstract class NetworkClientBase : INetEventListener, IClientConnection {
    /// <summary>底层 LiteNetLib 网络管理器。</summary>
    protected readonly NetManager _netClient;
    /// <summary>当前连接的服务端 peer；未连接时为 null。</summary>
    protected NetPeer? _serverPeer;
    /// <summary>日志记录器。</summary>
    protected readonly ILogger _logger;

    /// <summary>默认连接密钥，收口于 Battle.Entities.NetworkDefaults。</summary>
    public const string ConnectionKey = NetworkDefaults.ConnectionKey;
    /// <summary>默认服务端端口。</summary>
    protected const int DefaultPort = NetworkDefaults.LobbyPort;

    /// <summary>完全连接成功事件。</summary>
    public event Action? OnFullyConnected;
    /// <summary>完全断开连接事件。</summary>
    public event Action? OnFullyDisconnected;

    /// <summary>当前是否已连接到服务端。</summary>
    public bool IsConnected => _serverPeer != null;

    /// <param name="logger">日志记录器。</param>
    protected NetworkClientBase(ILogger logger) {
        _logger = logger;
        _netClient = new NetManager(this);
    }

    /// <summary>
    /// 使用默认连接密钥连接到指定主机。
    /// </summary>
    /// <param name="host">目标主机地址。</param>
    /// <param name="port">目标端口，默认使用 <see cref="DefaultPort"/>。</param>
    public virtual void Connect(string host, int port = DefaultPort) {
        Connect(host, port, ConnectionKey);
    }

    /// <summary>
    /// 使用自定义连接密钥连接。
    /// </summary>
    public virtual void Connect(string host, int port, string connectionKey) {
        _netClient.Start();
        _netClient.Connect(host, port, connectionKey);
    }

    /// <summary>
    /// 复用当前实例重连到新地址，不清空对象和事件订阅。
    /// 先执行 Disconnect 级别的清理，不触发 OnFullyDisconnected，再连接新地址。
    /// </summary>
    public virtual void Reconnect(string host, int port) {
        Reconnect(host, port, ConnectionKey);
    }

    /// <summary>
    /// 使用自定义连接密钥重连。
    /// </summary>
    public virtual void Reconnect(string host, int port, string connectionKey) {
        OnReconnectCleanup();
        _netClient.Start();
        _netClient.Connect(host, port, connectionKey);
    }

    /// <summary>子类重写以在重连时清理自身状态。</summary>
    protected virtual void OnReconnectCleanup() {
        _netClient.Stop();
        _serverPeer = null;
    }

    /// <summary>
    /// 断开与服务端的连接并停止网络监听。
    /// </summary>
    public virtual void Disconnect() {
        OnDisconnectCleanup();
        _netClient.Stop();
    }

    /// <summary>子类重写以在断开时清理自身状态。</summary>
    protected virtual void OnDisconnectCleanup() {
        _serverPeer = null;
    }

    /// <summary>
    /// 驱动网络事件轮询。
    /// 应由主循环每帧调用。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public virtual void Update(float delta) {
        _netClient.PollEvents();
        UpdateAfterPollEvents(delta);
    }

    /// <summary>子类重写以在 PollEvents 后执行额外逻辑，如 EntityManager.Update。</summary>
    protected virtual void UpdateAfterPollEvents(float delta) {
    }

    /// <summary>发送原始字节数据到服务端。</summary>
    public void SendCommand(byte[] messageBytes) {
        if (_serverPeer == null)
            return;
        _serverPeer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }

    #region INetEventListener 骨架

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        _serverPeer = peer;
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Connected. PeerId={PeerId}", peer.Id);
        OnPeerConnectedInternal(peer);
        OnFullyConnected?.Invoke();
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Disconnected. Reason={Reason}", disconnectInfo.Reason);
        OnPeerDisconnectedInternal(peer, disconnectInfo);
        OnFullyDisconnected?.Invoke();
    }

    /// <summary>子类重写以在连接建立时创建必要的管理器/订阅。</summary>
    protected virtual void OnPeerConnectedInternal(NetPeer peer) {
    }

    /// <summary>子类重写以在连接断开时清理自身资源。</summary>
    protected virtual void OnPeerDisconnectedInternal(NetPeer peer, DisconnectInfo disconnectInfo) {
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
    }
    void INetEventListener.OnConnectionRequest(ConnectionRequest request) => request.Reject();
    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }

    /// <summary>子类实现的网络接收入口。</summary>
    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var data = reader.GetRemainingBytes();
        OnNetworkReceiveInternal(data);
    }

    /// <summary>
    /// 子类实现的网络数据接收入口。
    /// </summary>
    /// <param name="data">接收到的原始字节数据。</param>
    protected abstract void OnNetworkReceiveInternal(ReadOnlySpan<byte> data);

    #endregion
}
