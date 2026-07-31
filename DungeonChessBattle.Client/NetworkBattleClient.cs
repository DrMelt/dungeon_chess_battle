using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Core.Network;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 基于 LiteEntitySystem 的网络战斗客户端，实现 IClientBattleService。
/// 状态同步由框架自动处理，客户端通过事件接收更新。
/// 实时化：完全使用 UnitPawn，不再依赖 UnitSyncEntity/EntityModelMapper。
/// </summary>
public class NetworkBattleClient : IClientBattleService, INetEventListener {
    private readonly NetManager _netClient;
    private ClientEntityManager? _entityManager;
    private NetPeer? _serverPeer;
    private readonly ILogger<NetworkBattleClient> _logger;

    private const int DefaultPort = 9050;
    private const string ConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;

    // Entity 缓存（加锁保护，后台线程写入，Godot 主线程读取）
    private readonly Dictionary<string, BattleRoomEntity> _rooms = [];
    private readonly Dictionary<string, List<UnitPawn>> _roomPawns = [];
    private readonly Lock _roomLock = new();

    // 待投递的事件队列（在后台线程收集，Update() 中统一投递）
    private readonly Queue<Action> _pendingEventInvocations = new();

    // Events for Godot
    public event Action<string, float, float>? UnitHealthChanged;
    public event Action<string>? UnitDied;
    public event Action<string, SyncBuffData>? UnitBuffAdded;
    public event Action<string, SyncBuffData>? UnitBuffRemoved;

    // 连接生命周期事件（通知 GameClientService 真正的连接/断开状态变化）
    public event Action? OnFullyConnected;
    public event Action? OnFullyDisconnected;

    // 房间操作响应事件（通知 UI 层）
    public event Action<string>? OnRoomJoined;
    public event Action<string>? OnRoomCreated;

    public bool IsConnected => _serverPeer != null;

    public NetworkBattleClient(ILogger<NetworkBattleClient> logger) {
        _logger = logger;
        _netClient = new NetManager(this);
    }

    public void Connect(string host, int port = DefaultPort) {
        _netClient.Start();
        _netClient.Connect(host, port, ConnectionKey);
    }

    public void Disconnect() {
        _netClient.Stop();
        _entityManager = null;
        _serverPeer = null;
        lock (_roomLock) {
            _rooms.Clear();
            _roomPawns.Clear();
        }
    }

    public void Update(float delta) {
        _ = delta; // 接口方法参数保留，未来可用于帧率适配
        _netClient.PollEvents();
        _entityManager?.Update();

        while (_pendingEventInvocations.TryDequeue(out var action)) {
            action();
        }
    }

    #region IClientBattleService

    public GameRoom? GetRoom(string roomId) {
        List<UnitPawn>? pawnsSnapshot;
        lock (_roomLock) {
            if (!_roomPawns.TryGetValue(roomId, out var pawns))
                return null;
            pawnsSnapshot = [.. pawns];
        }
        var room = new GameRoom(roomId);
        foreach (var p in pawnsSnapshot) {
            var model = BuildModelFromPawn(p);
            if (p.Camp.Value == (byte)Core.Enums.EnumCamp.Camp_A)
                room.UnitsA.Add(model);
            if (p.Camp.Value == (byte)Core.Enums.EnumCamp.Camp_B)
                room.UnitsB.Add(model);
        }
        return room;
    }

    public IEnumerable<GameRoom> GetAllRooms() {
        List<string> roomIds;
        lock (_roomLock) {
            roomIds = [.. _rooms.Where(kv => kv.Value != null).Select(kv => kv.Key)];
        }
        return [.. roomIds.Select(id => GetRoom(id)!).Where(r => r != null)];
    }

    public GameRoom CreateRoom(string roomId) {
        RequestCreateRoom(roomId);
        var room = new GameRoom(roomId);
        lock (_roomLock) {
            if (!_roomPawns.ContainsKey(roomId)) {
                _roomPawns[roomId] = [];
            }
        }
        return room;
    }

    public IUnitState CreateUnit(string roomId, string unitName, byte camp) {
        SendCommand(MessageWriter.WriteCreateUnit(roomId, unitName, camp));
        var model = new DungeonChessBattle.Core.Models.UnitModel { UnitStateName = unitName, Camp = (DungeonChessBattle.Core.Enums.EnumCamp)camp };
        return model;
    }

    public void CastSkill(string roomId, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null) {
        if (_entityManager == null)
            return;

        var casterPawn = FindPawnByName(caster.UnitStateName);
        var targetPawn = FindPawnByName(target.UnitStateName);
        if (casterPawn == null || targetPawn == null)
            return;

        bool isDamage = skill is SkillDamageModel;
        float damageOrCure = isDamage
            ? ((SkillDamageModel)skill).Damage
            : -((SkillCureModel)skill).CurePotency;
        byte damageType = (byte)(isDamage ? (byte)((SkillDamageModel)skill).DamageType : 0);

        var req = new SyncSkillRequest {
            CasterUnitNetId = casterPawn.Id,
            TargetUnitNetId = targetPawn.Id,
            IsDamage = isDamage,
            DamageOrCureValue = damageOrCure,
            DamageType = damageType,
        };
        casterPawn.RequestCastSkill(req);
    }

    public void UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime) {
        // Buff 结算由服务端权威执行，客户端仅接收同步更新
    }

    public bool CheckBattleEnded(string roomId) {
        lock (_roomLock) {
            return _rooms.TryGetValue(roomId, out var r) && r.IsFinished.Value;
        }
    }

    #endregion

    #region INetEventListener

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Client] Connected. PeerId={PeerId}", peer.Id);
        _serverPeer = peer;
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ClientEntityManager(typesMap, lesPeer, PacketHeader);

        // 订阅所有同步 Entity 类型的创建事件
        _entityManager.GetEntities<BattleRoomEntity>()
            .SubscribeToConstructed(OnRoomEntityCreated, callOnExisting: true);
        _entityManager.GetEntities<UnitPawn>()
            .SubscribeToConstructed(OnPawnEntityCreated, callOnExisting: true);
        _entityManager.GetEntities<PlayerRoomEntity>()
            .SubscribeToConstructed(OnPlayerEntityCreated, callOnExisting: true);

        OnFullyConnected?.Invoke();
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo info) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Client] Disconnected. Reason={Reason}", info.Reason);
        _entityManager = null;
        _serverPeer = null;

        lock (_roomLock) {
            _rooms.Clear();
            _roomPawns.Clear();
        }

        OnFullyDisconnected?.Invoke();
    }

    void INetEventListener.OnNetworkError(IPEndPoint ep, SocketError err) {
    }
    void INetEventListener.OnConnectionRequest(ConnectionRequest r) => r.Reject();

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod delivery) {
        var data = reader.GetRemainingBytes();
        if (data.Length > 0 && data[0] == PacketHeader) {
            _entityManager?.Deserialize(data);
        }
        else {
            HandleCustomPacket(data);
        }
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint ep, NetPacketReader r, UnconnectedMessageType t) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }

    #endregion

    #region Helpers

    public void RequestCreateRoom(string roomId) {
        SendCommand(MessageWriter.WriteRoomRequest(MessageType.CreateRoom, roomId));
    }

    public void RequestJoinRoom(string roomId) {
        SendCommand(MessageWriter.WriteRoomRequest(MessageType.JoinRoom, roomId));
    }

    private void SendCommand(byte[] messageBytes) {
        if (_serverPeer == null)
            return;
        _serverPeer.Send(messageBytes, DeliveryMethod.ReliableOrdered);
    }

    private UnitPawn? FindPawnByName(string unitName) {
        lock (_roomLock) {
            foreach (var (_, pawns) in _roomPawns) {
                var match = pawns.Find(p => p.UnitName.Value == unitName);
                if (match != null)
                    return match;
            }
        }
        return null;
    }

    private static UnitModel BuildModelFromPawn(UnitPawn p) {
        return new UnitModel {
            UnitStateName = p.UnitName.Value,
            Health = p.Health.Value,
            MaxHealth = p.MaxHealth.Value,
            PhysicalAttackBase = p.PhysicalAttackBase.Value,
            MagicAttackBase = p.MagicAttackBase.Value,
            PhysicalTakePercent = p.PhysicalTakePercent.Value,
            MagicTakePercent = p.MagicTakePercent.Value,
            CureIntensity = p.CureIntensity.Value,
            BaseSpeed = p.BaseSpeed.Value,
        };
    }

    #endregion

    #region Entity Callbacks

    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        lock (_roomLock) {
            _rooms[entity.RoomId.Value] = entity;
            _roomPawns[entity.RoomId.Value] = [];
        }
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Client] Room entity created: {RoomId}", entity.RoomId.Value);
    }

    private void OnPawnEntityCreated(UnitPawn pawn) {
        var unitName = pawn.UnitName.Value;
        lock (_roomLock) {
            foreach (var (roomId, _) in _rooms) {
                if (unitName.StartsWith(roomId)) {
                    if (!_roomPawns.TryGetValue(roomId, out var list)) {
                        list = [];
                        _roomPawns[roomId] = list;
                    }
                    list.Add(pawn);
                    break;
                }
            }
            // fallback: 加入最后一个房间
            var lastRoomId = _rooms.Keys.LastOrDefault();
            if (lastRoomId != null && !_roomPawns.Values.Any(list => list.Contains(pawn))) {
                if (!_roomPawns.TryGetValue(lastRoomId, out var list)) {
                    list = [];
                    _roomPawns[lastRoomId] = list;
                }
                list.Add(pawn);
            }
        }

        // 订阅 UnitPawn 事件（延迟到 Update() 中投递以保护订阅者的线程安全）
        pawn.HealthChanged += (u, newHealth, oldHealth) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitHealthChanged?.Invoke(u.UnitName.Value, newHealth, oldHealth));
        pawn.UnitDied += (u) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitDied?.Invoke(u.UnitName.Value));
        pawn.BuffAdded += (u, buff) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitBuffAdded?.Invoke(u.UnitName.Value, buff));
        pawn.BuffRemoved += (u, buff) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitBuffRemoved?.Invoke(u.UnitName.Value, buff));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Client] UnitPawn entity created: {UnitName}, Camp={Camp}, Pos={Position}", unitName, pawn.Camp.Value, pawn.Position.Value);
    }

    private void OnPlayerEntityCreated(PlayerRoomEntity player) {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Client] Player entity created: {PlayerName}", player.PlayerName.Value);
    }

    #endregion

    #region Custom Packet Handling

    private void HandleCustomPacket(ReadOnlySpan<byte> data) {
        try {
            string json = Encoding.UTF8.GetString(data);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            string? type = root.TryGetProperty(MessageProperty.Type, out var tp) ? tp.GetString() : null;

            switch (type) {
                case MessageType.JoinRoomResponse:
                    HandleJoinRoomResponse(root);
                    break;
                case MessageType.CreateRoomResponse:
                    HandleCreateRoomResponse(root);
                    break;
                default:
                    if (_logger.IsEnabled(LogLevel.Warning))
                        _logger.LogWarning("[Client] Unknown custom packet: {Type}", type);
                    break;
            }
        }
        catch (Exception ex) {
            _logger.LogWarning(ex, "[Client] Custom packet parse error");
        }
    }

    private void HandleJoinRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;
        string? error = root.TryGetProperty(MessageProperty.Error, out var ep) ? ep.GetString() : null;

        if (success && !string.IsNullOrEmpty(roomId)) {
            _pendingEventInvocations.Enqueue(() => OnRoomJoined?.Invoke(roomId));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Client] Join room succeeded: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[Client] Join room failed: {Error}", error ?? "unknown");
        }
    }

    private void HandleCreateRoomResponse(JsonElement root) {
        bool success = root.TryGetProperty(MessageProperty.Success, out var sp) && sp.GetBoolean();
        string? roomId = root.TryGetProperty(MessageProperty.RoomId, out var rp) ? rp.GetString() : null;

        if (success && !string.IsNullOrEmpty(roomId)) {
            _pendingEventInvocations.Enqueue(() => OnRoomCreated?.Invoke(roomId));
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Client] Create room succeeded: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[Client] Create room failed");
        }
    }

    #endregion
}
