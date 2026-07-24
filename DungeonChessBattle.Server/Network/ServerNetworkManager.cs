using System.Text.Json;
using LiteNetLib;
using LiteNetLib.Utils;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 服务器网络管理器，封装 LiteNetLib 的连接管理和消息收发。
/// </summary>
public class ServerNetworkManager {
    private readonly NetManager _netManager;
    private readonly EventBasedNetListener _listener;
    private readonly Dictionary<int, NetPeer> _peers = [];
    private const int DefaultPort = 9050;

    /// <summary>
    /// 收到消息时触发。参数: (peerId, jsonMessage)
    /// </summary>
    public event Action<int, string>? OnMessageReceived;

    /// <summary>
    /// 客户端连接时触发。
    /// </summary>
    public event Action<int>? OnClientConnected;

    /// <summary>
    /// 客户端断开时触发。
    /// </summary>
    public event Action<int>? OnClientDisconnected;

    public ServerNetworkManager() {
        _listener = new EventBasedNetListener();
        _netManager = new NetManager(_listener);

        _listener.ConnectionRequestEvent += OnConnectionRequest;
        _listener.PeerConnectedEvent += OnPeerConnected;
        _listener.PeerDisconnectedEvent += OnPeerDisconnected;
        _listener.NetworkReceiveEvent += OnNetworkReceive;
    }

    public void Start() {
        _netManager.Start(DefaultPort);
        Console.WriteLine($"[Network] Listening on port {DefaultPort}");
    }

    public void Stop() {
        _netManager.Stop();
        _peers.Clear();
        Console.WriteLine("[Network] Stopped");
    }

    public void PollEvents() {
        _netManager.PollEvents();
    }

    /// <summary>
    /// 向指定客户端发送 JSON 消息。
    /// </summary>
    public void SendToClient(int peerId, string jsonMessage) {
        if (_peers.TryGetValue(peerId, out var peer)) {
            var writer = new NetDataWriter();
            writer.Put(jsonMessage);
            peer.Send(writer, DeliveryMethod.ReliableOrdered);
        }
    }

    /// <summary>
    /// 向指定客户端发送带 RequestId 的 JSON 响应。
    /// </summary>
    public void SendResponse(int peerId, int requestId, object result) {
        var json = JsonSerializer.Serialize(new { RequestId = requestId, Result = result });
        SendToClient(peerId, json);
    }

    public int PeerCount => _peers.Count;

    private void OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey("DungeonChessBattle");
    }

    private void OnPeerConnected(NetPeer peer) {
        _peers[peer.Id] = peer;
        Console.WriteLine($"[Network] Peer connected: {peer.Id}");
        OnClientConnected?.Invoke(peer.Id);
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        _peers.Remove(peer.Id);
        Console.WriteLine($"[Network] Peer disconnected: {peer.Id}, Reason: {disconnectInfo.Reason}");
        OnClientDisconnected?.Invoke(peer.Id);
    }

    private void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var json = reader.GetString();
        Console.WriteLine($"[Network] Received from {peer.Id}: {json[..Math.Min(json.Length, 120)]}...");
        OnMessageReceived?.Invoke(peer.Id, json);
    }
}