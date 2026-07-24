using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using LiteNetLib;
using LiteNetLib.Utils;

namespace DungeonChessBattle.Client;

/// <summary>
/// 网络战斗客户端，实现 IBattleService。
/// 将本地调用序列化为网络消息发送到服务端，接收服务端返回值。
/// </summary>
public class NetworkBattleClient : IBattleService, INetEventListener {
    private readonly NetManager _client;
    private NetPeer? _serverPeer;
    private readonly Dictionary<int, TaskCompletionSource<string>> _pendingRequests = [];
    private int _requestId;

    private const int DefaultPort = 9050;
    private const string ConnectionKey = "DungeonChessBattle";

    public NetworkBattleClient() {
        _client = new NetManager(this);
    }

    public void Connect(string host, int port = DefaultPort) {
        _client.Start();
        _serverPeer = _client.Connect(host, port, ConnectionKey);
    }

    public void Disconnect() {
        _client.Stop();
        _serverPeer = null;
    }

    public void PollEvents() {
        _client.PollEvents();
    }

    #region IBattleService Implementation

    public GameRoom CreateRoom(string roomId) => SendRequest<GameRoom>("CreateRoom", roomId);

    public GameRoom? GetRoom(string roomId) => SendRequest<GameRoom?>("GetRoom", roomId);

    public bool RemoveRoom(string roomId) => SendRequest<bool>("RemoveRoom", roomId);

    public IEnumerable<GameRoom> GetAllRooms() => SendRequest<List<GameRoom>>("GetAllRooms");

    public BattleManager StartBattleInRoom(string roomId) => SendRequest<BattleManager>("StartBattleInRoom", roomId);

    public BattleManager? GetBattle(string roomId) => SendRequest<BattleManager?>("GetBattle", roomId);

    public void AdvancePhase(BattleManager battle) => SendCommand("AdvancePhase", battle);

    public void NextRound(BattleManager battle) => SendCommand("NextRound", battle);

    public void EndBattle(BattleManager battle) => SendCommand("EndBattle", battle);

    public void CastSkill(BattleManager battle, UnitModel caster, UnitModel target, SkillModel skill) {
        var payload = JsonSerializer.Serialize(new {
            BattleId = battle.GetHashCode(),
            CasterId = caster.GetHashCode(),
            TargetId = target.GetHashCode(),
            SkillType = skill.GetType().Name,
            SkillData = skill
        });
        SendCommand("CastSkill", payload);
    }

    public void UpdateBuffs(BattleManager battle, IEnumerable<UnitModel> units, double deltaTime) {
        var unitIds = units.Select(u => u.GetHashCode()).ToArray();
        var payload = JsonSerializer.Serialize(new { UnitIds = unitIds, DeltaTime = deltaTime });
        SendCommand("UpdateBuffs", payload);
    }

    public bool CheckBattleEnded(GameRoom room) => SendRequest<bool>("CheckBattleEnded", room.RoomId);

    #endregion

    #region Network Send

    private void SendCommand(string method, object payload) {
        var writer = new NetDataWriter();
        var json = JsonSerializer.Serialize(new { Method = method, Payload = payload });
        writer.Put(json);
        _serverPeer?.Send(writer, DeliveryMethod.ReliableOrdered);
    }

    private T SendRequest<T>(string method, params object[] args) {
        if (_serverPeer == null)
            throw new InvalidOperationException("Not connected to server.");

        int id = Interlocked.Increment(ref _requestId);
        var tcs = new TaskCompletionSource<string>();
        lock (_pendingRequests) {
            _pendingRequests[id] = tcs;
        }

        var writer = new NetDataWriter();
        var json = JsonSerializer.Serialize(new { RequestId = id, Method = method, Args = args });
        writer.Put(json);
        _serverPeer.Send(writer, DeliveryMethod.ReliableOrdered);

        var result = tcs.Task.Result;
        return JsonSerializer.Deserialize<T>(result)!;
    }

    #endregion

    #region INetEventListener

    public void OnPeerConnected(NetPeer peer) {
        _serverPeer = peer;
    }

    public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (_serverPeer == peer)
            _serverPeer = null;
    }

    public void OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
    }

    public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var json = reader.GetString();
        var message = JsonSerializer.Deserialize<JsonElement>(json);

        if (message.TryGetProperty("RequestId", out var reqIdProp)) {
            int reqId = reqIdProp.GetInt32();
            lock (_pendingRequests) {
                if (_pendingRequests.TryGetValue(reqId, out var tcs)) {
                    _pendingRequests.Remove(reqId);
                    tcs.SetResult(json);
                }
            }
        }
    }

    public void OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }

    public void OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }

    public void OnConnectionRequest(ConnectionRequest request) {
        request.Reject();
    }

    #endregion
}