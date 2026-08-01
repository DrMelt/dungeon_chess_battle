using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 房间战斗客户端，负责与房间端口的 LES 二进制协议 (0xDC) 通信。
/// 实现 IClientBattleService，管理 LES Entity（BattleRoomEntity、UnitPawn、PlayerRoomEntity）。
/// 不包含大厅 JSON 协议。
/// </summary>
public class RoomBattleClient(ILogger<RoomBattleClient> logger) : NetworkClientBase(logger), IClientBattleService {
    private ClientEntityManager? _entityManager;

    private const byte PacketHeader = 0xDC;

    // Entity 缓存（加锁保护，后台线程写入，Godot 主线程读取）
    private readonly Dictionary<string, BattleRoomEntity> _rooms = [];
    private readonly Dictionary<string, List<UnitPawn>> _roomPawns = [];
    private readonly Lock _roomLock = new();

    // ── 战斗事件（通知 UI 层） ──
    public event Action<string, float, float>? UnitHealthChanged;
    public event Action<string>? UnitDied;
    public event Action<string, SyncBuffData>? UnitBuffAdded;
    public event Action<string, SyncBuffData>? UnitBuffRemoved;

    /// <summary>
    /// 战斗阶段变化事件（roomId, phase）。
    /// 由 BattleRoomEntity.BattlePhase SyncVar 变化触发。
    /// </summary>
    public event Action<string, BattlePhase>? BattlePhaseChanged;

    // 本地玩家的 UnitController（在 OnPlayerEntityCreated 中查找并保存）
    private UnitController? _localController;

    // ── Reconnect 清理 ──

    protected override void OnReconnectCleanup() {
        base.OnReconnectCleanup();
        _entityManager = null;
        lock (_roomLock) {
            _rooms.Clear();
            _roomPawns.Clear();
        }
    }

    protected override void OnDisconnectCleanup() {
        base.OnDisconnectCleanup();
        _entityManager = null;
        lock (_roomLock) {
            _rooms.Clear();
            _roomPawns.Clear();
        }
    }

    // ── Update ──

    protected override void OnAfterPollEvents() {
        _entityManager?.Update();
    }

    // ── OnNetworkReceive（只处理 LES 0xDC 包） ──

    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        if (data.Length > 0 && data[0] == PacketHeader) {
            _entityManager?.Deserialize(data);
        }
        // 房间端口不处理 JSON，其余丢弃
    }

    // ── 连接生命周期 ──

    protected override void OnPeerConnectedInternal(NetPeer peer) {
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

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] LES EntityManager created for peer {PeerId}", peer.Id);
    }

    protected override void OnPeerDisconnectedInternal(NetPeer peer, DisconnectInfo info) {
        _entityManager = null;
        lock (_roomLock) {
            _rooms.Clear();
            _roomPawns.Clear();
        }
    }

    // ── IClientBattleService ──

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
        // 网络模式下通过大厅客户端发送 JSON 请求，此处返回空壳
        var room = new GameRoom(roomId);
        lock (_roomLock) {
            if (!_roomPawns.ContainsKey(roomId)) {
                _roomPawns[roomId] = [];
            }
        }
        return room;
    }

    public IUnitState CreateUnit(string roomId, string unitName, byte camp) {
        BattleRoomEntity? roomEntity;
        lock (_roomLock) {
            _rooms.TryGetValue(roomId, out roomEntity);
        }
        if (roomEntity != null) {
            var req = new SyncCreateUnitRequest { UnitName = unitName, Camp = camp };
            roomEntity.RequestCreateUnit(req);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomBattleClient] CreateUnit: room entity not found for {RoomId}", roomId);
        }

        var model = new UnitModel { UnitStateName = unitName, Camp = (Core.Enums.EnumCamp)camp };
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

    // ── 玩家输入 ──

    /// <summary>
    /// Godot UI 层调用，提交当前帧的玩家输入到 LES 框架。
    /// 框架自动进行 Delta 编码、UDP 发送、预测回滚。
    /// </summary>
    public void SubmitPlayerInput(System.Numerics.Vector2 moveDir, byte skillFlags, System.Numerics.Vector2 aimPos) {
        _localController?.SubmitInput(moveDir, skillFlags, aimPos);
    }

    // ── RPC 请求 ──

    public void RequestStartBattle(string roomId) {
        BattleRoomEntity? roomEntity;
        lock (_roomLock) {
            _rooms.TryGetValue(roomId, out roomEntity);
        }
        if (roomEntity != null) {
            roomEntity.RequestStartBattle();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomBattleClient] RequestStartBattle via RPC: {RoomId}", roomId);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomBattleClient] RequestStartBattle: room entity not found for {RoomId}", roomId);
        }
    }

    // ── Entity 回调 ──

    private void OnRoomEntityCreated(BattleRoomEntity entity) {
        lock (_roomLock) {
            _rooms[entity.RoomId.Value] = entity;
            _roomPawns[entity.RoomId.Value] = [];
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Room entity created: {RoomId}", entity.RoomId.Value);
    }

    private void OnPawnEntityCreated(UnitPawn pawn) {
        var unitName = pawn.UnitName.Value;
        lock (_roomLock) {
            var roomId = _rooms.Keys.FirstOrDefault();
            if (roomId != null) {
                if (!_roomPawns.TryGetValue(roomId, out var list)) {
                    list = [];
                    _roomPawns[roomId] = list;
                }
                list.Add(pawn);
            }
            else {
                if (_logger.IsEnabled(LogLevel.Warning))
                    _logger.LogWarning("[RoomBattleClient] Pawn '{UnitName}' arrived before room entity was created.", unitName);
            }
        }

        // 订阅 UnitPawn 事件
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
            _logger.LogInformation("[RoomBattleClient] UnitPawn entity created: {UnitName}, Camp={Camp}, Pos={Position}",
                unitName, pawn.Camp.Value, pawn.Position.Value);
    }

    private void OnPlayerEntityCreated(PlayerRoomEntity player) {
        // 尝试查找并保存本地玩家的 UnitController（用于 SubmitPlayerInput）
        if (_entityManager != null && _localController == null) {
            _localController = _entityManager.GetPlayerController<UnitController>();
            if (_localController != null && _logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomBattleClient] Local UnitController found for player: {PlayerName}", player.PlayerName.Value);
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Player entity created: {PlayerName}", player.PlayerName.Value);
    }

    // ── 辅助 ──

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
            Camp = (Core.Enums.EnumCamp)p.Camp.Value,
        };
    }
}
