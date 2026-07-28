using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.Mapper;
using DungeonChessBattle.Entities.SyncData;

namespace DungeonChessBattle.Client;

/// <summary>
/// 基于 LiteEntitySystem 的网络战斗客户端，实现 IClientBattleService。
/// 状态同步由框架自动处理，客户端通过事件接收更新。
/// </summary>
public class NetworkBattleClient : IClientBattleService, INetEventListener {
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

    #region IClientBattleService

    public GameRoom? GetRoom(string roomId) {
        _roomUnits.TryGetValue(roomId, out var units);
        if (units == null)
            return null;
        var room = new GameRoom(roomId);
        foreach (var u in units) {
            var model = BuildModelFromSync(u);
            if (u.Camp.Value == 1)
                room.UnitsA.Add(model);
            if (u.Camp.Value == 2)
                room.UnitsB.Add(model);
        }
        return room;
    }

    public IEnumerable<GameRoom> GetAllRooms()
        => _rooms.Keys.Select(id => GetRoom(id)!).Where(r => r != null);

    public void CastSkill(string roomId, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null) {
        if (_entityManager == null)
            return;

        var casterEntity = FindUnitEntityByName(caster.UnitStateName);
        var targetEntity = FindUnitEntityByName(target.UnitStateName);
        if (casterEntity == null || targetEntity == null)
            return;

        bool isDamage = skill is SkillDamageModel;
        float damageOrCure = isDamage
            ? ((SkillDamageModel)skill).Damage
            : -((SkillCureModel)skill).CurePotency;
        byte damageType = (byte)(isDamage ? (byte)((SkillDamageModel)skill).DamageType : 0);

        var req = new SyncSkillRequest {
            CasterUnitNetId = casterEntity.Id,
            TargetUnitNetId = targetEntity.Id,
            IsDamage = isDamage,
            DamageOrCureValue = damageOrCure,
            DamageType = damageType,
        };
        casterEntity.RequestCastSkill(req);
    }

    /// <summary>
    /// 客户端不独立结算 Buff —— 服务端权威结算后通过 UnitSyncEntity.ServerSetHealth 下推结果。
    /// 本地 UI 通过订阅 UnitHealthChanged / UnitBuffAdded / UnitBuffRemoved 事件获取更新。
    /// </summary>
    public void UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime) {
        // Buff 结算由服务端权威执行，客户端仅接收同步更新
    }

    public bool CheckBattleEnded(string roomId)
        => _rooms.TryGetValue(roomId, out var r) && r.IsFinished.Value;

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

    /// <summary>
    /// 请求服务端创建房间。
    /// </summary>
    public void RequestCreateRoom(string roomId) {
        SendCommand(new { type = "create_room", roomId });
    }

    /// <summary>
    /// 请求加入已有房间。
    /// </summary>
    public void RequestJoinRoom(string roomId) {
        SendCommand(new { type = "join_room", roomId });
    }

    private void SendCommand(object command) {
        if (_serverPeer == null)
            return;
        string json = JsonSerializer.Serialize(command);
        byte[] data = Encoding.UTF8.GetBytes(json);
        _serverPeer.Send(data, DeliveryMethod.ReliableOrdered);
    }

    private UnitSyncEntity? FindUnitEntityByName(string unitName) {
        foreach (var (_, units) in _roomUnits) {
            var match = units.Find(u => u.UnitName.Value == unitName);
            if (match != null)
                return match;
        }
        return null;
    }

    private static UnitModel BuildModelFromSync(UnitSyncEntity u) => EntityModelMapper.FromSyncEntity(u);

    #endregion

    #region Entity Callbacks

    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        _rooms[entity.RoomId.Value] = entity;
        _roomUnits[entity.RoomId.Value] = [];
        Console.WriteLine($"[Client] Room entity created: {entity.RoomId.Value}");
    }

    private void OnUnitEntityCreated(UnitSyncEntity unit) {
        // 根据所属房间缓存（简单策略：遍历所有房间，加入最近创建的房间）
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
