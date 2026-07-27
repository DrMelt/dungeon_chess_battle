using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Client;

/// <summary>
/// 基于 LiteEntitySystem 的网络战斗客户端。
/// 状态同步由框架自动处理，客户端通过事件接收更新。
/// </summary>
public class NetworkBattleClient : IBattleService, INetEventListener {
    private readonly NetManager _netClient;
    private ClientEntityManager? _entityManager;
    private NetPeer? _serverPeer;

    private const int DefaultPort = 9050;
    private const string ConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;

    // Entity 缓存
    private readonly Dictionary<string, BattleRoomEntity> _rooms = [];
    private readonly Dictionary<string, List<UnitSyncEntity>> _roomUnits = [];

    // Events for Godot
    public event Action<string, float, float>? UnitHealthChanged;
    public event Action<string>? UnitDied;
    public event Action<string, SyncBuffData>? UnitBuffAdded;
    public event Action<string, SyncBuffData>? UnitBuffRemoved;

    public bool IsConnected => _serverPeer != null;

    public NetworkBattleClient() {
        _netClient = new NetManager(this);
    }

    public void Connect(string host, int port = DefaultPort) {
        _netClient.Start();
        _serverPeer = _netClient.Connect(host, port, ConnectionKey);
    }

    public void Disconnect() {
        _netClient.Stop();
        _entityManager = null;
        _serverPeer = null;
        _rooms.Clear();
        _roomUnits.Clear();
    }

    public void Update(float _) {
        _entityManager?.Update();
    }

    #region IBattleService

    public GameRoom CreateRoom(string roomId)
        => throw new NotSupportedException("Client cannot create rooms. Use server CLI.");

    public GameRoom? GetRoom(string roomId) {
        _roomUnits.TryGetValue(roomId, out var units);
        if (units == null)
            return null;
        var room = new GameRoom(roomId);
        foreach (var u in units) {
            if (u.Camp.Value == 1)
                room.UnitsA.Add(new UnitModel { UnitStateName = u.UnitName.Value });
            if (u.Camp.Value == 2)
                room.UnitsB.Add(new UnitModel { UnitStateName = u.UnitName.Value });
        }
        return room;
    }

    public bool RemoveRoom(string roomId) {
        _rooms.Remove(roomId);
        _roomUnits.Remove(roomId);
        return true;
    }
    public IEnumerable<GameRoom> GetAllRooms()
        => _rooms.Keys.Select(id => GetRoom(id)!).Where(r => r != null);

    public BattleManager StartBattleInRoom(string roomId) {
        SendCommand(new {
            type = "start_battle", roomId
        });
        return new BattleManager();
    }

    public BattleManager? GetBattle(string roomId) => null;

    public void AdvancePhase(BattleManager b) => SendCommand(new { type = "advance_phase" });

    public void NextRound(BattleManager b) => SendCommand(new { type = "next_round" });

    public void EndBattle(BattleManager b) => SendCommand(new { type = "end_battle" });

    public void CastSkill(BattleManager battle, UnitModel caster, UnitModel target, SkillModel skill) {
        if (_serverPeer == null)
            return;

        // 发送自定义 JSON 包（不走 LES），直接携带技能参数
        var request = new {
            type = "cast_skill",
            casterName = caster.UnitStateName,
            targetName = target.UnitStateName,
            isDamage = skill is SkillDamageModel,
            damage = (skill as SkillDamageModel)?.Damage ?? 0f,
            damageType = (int)((skill as SkillDamageModel)?.DamageType ?? 0),
            cure = (skill as SkillCureModel)?.CurePotency ?? 0f
        };
        string json = JsonSerializer.Serialize(request);
        byte[] data = Encoding.UTF8.GetBytes(json);
        _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
    }

    public void UpdateBuffs(BattleManager b, IEnumerable<UnitModel> u, double dt) {
    }

    public bool CheckBattleEnded(GameRoom room)
        => _rooms.TryGetValue(room.RoomId, out var r) && r.IsFinished.Value;

    #endregion

    #region INetEventListener

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        Console.WriteLine($"[Client] Connected. PeerId={peer.Id}");
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ClientEntityManager(typesMap, lesPeer, PacketHeader);

        // 订阅所有同步 Entity 类型的创建事件
        _entityManager.GetEntities<BattleRoomEntity>()
            .SubscribeToConstructed(OnRoomEntityCreated, callOnExisting: false);
        _entityManager.GetEntities<UnitSyncEntity>()
            .SubscribeToConstructed(OnUnitEntityCreated, callOnExisting: false);
        _entityManager.GetEntities<PlayerRoomEntity>()
            .SubscribeToConstructed(OnPlayerEntityCreated, callOnExisting: false);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info) {
        Console.WriteLine($"[Client] Disconnected.");
        _entityManager = null;
        _serverPeer = null;
        _rooms.Clear();
        _roomUnits.Clear();
    }

    void INetEventListener.OnNetworkError(IPEndPoint ep, SocketError err) {
    }
    void INetEventListener.OnConnectionRequest(ConnectionRequest r) => r.Reject();

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery) {
        _entityManager?.Deserialize(reader.GetRemainingBytes());
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint ep, NetPacketReader r, UnconnectedMessageType t) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }

    #endregion

    #region Helpers

    private void SendCommand(object command) {
        if (_serverPeer == null)
            return;
        string json = JsonSerializer.Serialize(command);
        byte[] data = Encoding.UTF8.GetBytes(json);
        _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
    }

    #endregion

    #region Entity Callbacks

    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        _rooms[entity.RoomId.Value] = entity;
        _roomUnits[entity.RoomId.Value] = [];
        Console.WriteLine($"[Client] Room entity created: {entity.RoomId.Value}");
    }

    private void OnUnitEntityCreated(UnitSyncEntity unit) {
        // 根据所属房间缓存（简单策略：遍历所有房间，加入最近创建的房间）
        // 更好的方式是通过服务端发送 roomId 关联，此处暂时用 Name 前缀查找
        var unitName = unit.UnitName.Value;
        foreach (var (roomId, room) in _rooms) {
            if (unitName.StartsWith(roomId)) {
                _roomUnits[roomId].Add(unit);
                break;
            }
        }
        // 如果没匹配到，加入最后一个房间
        if (!_roomUnits.Values.Any(list => list.Contains(unit))) {
            var lastRoomId = _rooms.Keys.LastOrDefault();
            if (lastRoomId != null)
                _roomUnits[lastRoomId].Add(unit);
        }

        // 订阅单位事件，转发到公开事件
        unit.HealthChanged += (u, newHealth, oldHealth) =>
            UnitHealthChanged?.Invoke(u.UnitName.Value, newHealth, oldHealth);
        unit.UnitDied += (u) =>
            UnitDied?.Invoke(u.UnitName.Value);
        unit.BuffAdded += (u, buff) =>
            UnitBuffAdded?.Invoke(u.UnitName.Value, buff);
        unit.BuffRemoved += (u, buff) =>
            UnitBuffRemoved?.Invoke(u.UnitName.Value, buff);

        Console.WriteLine($"[Client] Unit entity created: {unit.UnitName.Value}, Camp={unit.Camp.Value}");
    }

    private void OnPlayerEntityCreated(PlayerRoomEntity player) {
        Console.WriteLine($"[Client] Player entity created: {player.PlayerName.Value}");
    }

    #endregion
}
