using LiteNetLib;

namespace DungeonChessBattle.Server.Network;

public class ServerNetworkManager
{
    private readonly NetManager _netManager;
    private const int DefaultPort = 9050;

    public ServerNetworkManager()
    {
        var listener = new EventBasedNetListener();
        _netManager = new NetManager(listener);

        listener.ConnectionRequestEvent += OnConnectionRequest;
        listener.PeerConnectedEvent += OnPeerConnected;
        listener.PeerDisconnectedEvent += OnPeerDisconnected;
    }

    public void Start()
    {
        _netManager.Start(DefaultPort);
        Console.WriteLine($"[Network] Listening on port {DefaultPort}");
    }

    public void Stop()
    {
        _netManager.Stop();
        Console.WriteLine("[Network] Stopped");
    }

    private void OnConnectionRequest(ConnectionRequest request)
    {
        request.AcceptIfKey("DungeonChessBattle");
    }

    private void OnPeerConnected(NetPeer peer)
    {
        Console.WriteLine($"[Network] Peer connected: {peer.Id}");
    }

    private void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
    {
        Console.WriteLine($"[Network] Peer disconnected: {peer.Id}, Reason: {disconnectInfo.Reason}");
    }
}