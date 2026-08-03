using System.Net;
using System.Net.Sockets;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 客户端网络基础设施抽象基类。
/// 提供 NetManager、Connect/Disconnect/Reconnect/Update、INetEventListener 骨架。
/// 子类重写 OnNetworkReceive、OnPeerConnected、OnPeerDisconnected 实现具体协议。
/// </summary>
public abstract class NetworkClientBase : INetEventListener {
    protected readonly NetManager _netClient;
    protected NetPeer? _serverPeer;
    protected readonly ILogger _logger;

    public const string ConnectionKey = "DungeonChessBattle";
    protected const int DefaultPort = 10170;

    // 待投递的事件队列（网络线程入队，Update() 线程出队，需线程安全）
    protected readonly System.Collections.Concurrent.ConcurrentQueue<Action> _pendingEventInvocations = new();

    // 连接生命周期事件
    public event Action? OnFullyConnected;
    public event Action? OnFullyDisconnected;

    public bool IsConnected => _serverPeer != null;

    protected NetworkClientBase(ILogger logger) {
        _logger = logger;
        _netClient = new NetManager(this);
    }

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
    /// 复用当前实例重连到新地址（不清空对象和事件订阅）。
    /// 先执行 Disconnect 级别的清理（不触发 OnFullyDisconnected），再连接新地址。
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

    public virtual void Disconnect() {
        OnDisconnectCleanup();
        _netClient.Stop();
    }

    /// <summary>子类重写以在断开时清理自身状态。</summary>
    protected virtual void OnDisconnectCleanup() {
        _serverPeer = null;
    }

    public virtual void Update(float delta) {
        _ = delta;
        _netClient.PollEvents();
        OnAfterPollEvents();

        while (_pendingEventInvocations.TryDequeue(out var action)) {
            action();
        }
    }

    /// <summary>子类重写以在 PollEvents 后执行额外逻辑（如 EntityManager.Update）。</summary>
    protected virtual void OnAfterPollEvents() {
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
            _logger.LogInformation("[Client] Connected. PeerId={PeerId}", peer.Id);
        OnPeerConnectedInternal(peer);
        OnFullyConnected?.Invoke();
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Client] Disconnected. Reason={Reason}", info.Reason);
        OnPeerDisconnectedInternal(peer, info);
        OnFullyDisconnected?.Invoke();
    }

    /// <summary>子类重写以在连接建立时创建必要的管理器/订阅。</summary>
    protected virtual void OnPeerConnectedInternal(NetPeer peer) {
    }

    /// <summary>子类重写以在连接断开时清理自身资源。</summary>
    protected virtual void OnPeerDisconnectedInternal(NetPeer peer, DisconnectInfo info) {
    }

    void INetEventListener.OnNetworkError(IPEndPoint ep, SocketError err) {
    }
    void INetEventListener.OnConnectionRequest(ConnectionRequest r) => r.Reject();
    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint ep, NetPacketReader r, UnconnectedMessageType t) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }

    // ── 子类必须重写 ──
    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery) {
        var data = reader.GetRemainingBytes();
        OnNetworkReceiveInternal(data);
    }

    protected abstract void OnNetworkReceiveInternal(ReadOnlySpan<byte> data);

    #endregion
}
