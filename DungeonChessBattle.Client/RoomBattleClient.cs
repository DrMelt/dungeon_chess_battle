using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 房间战斗客户端，负责与房间端口的 LES 二进制协议 (0xDC) 通信。
/// 实现 IClientBattleService，管理 LES Entity（BattleRoomEntity、UnitPawn、PlayerRoomEntity）。
/// 不包含大厅 JSON 协议。
/// 客户端同时只连接一个房间，使用单实例字段替代多房间 Dictionary（P2-7 优化）。
/// </summary>
public class RoomBattleClient(ILogger<RoomBattleClient> logger) : NetworkClientBase(logger), IClientBattleService {
    private ClientEntityManager? _entityManager;

    private const byte PacketHeader = 0xDC;

    // ── 单房间 Entity 缓存（P2-7：替代 Dictionary） ──
    private BattleRoomEntity? _roomEntity;
    private readonly List<UnitPawn> _roomPawns = [];
    private string? _currentRoomId;
    private readonly Lock _lock = new();

    // ── 接口战斗事件（IClientBattleService） ──
    public event Action<string, float, float>? UnitHealthChanged;
    public event Action<string>? UnitDied;
    public event Action<string, BuffEventData>? UnitBuffAdded;
    public event Action<string, BuffEventData>? UnitBuffRemoved;

    // ── 接口事件（IClientBattleService） ──
    /// <summary>单位创建事件。参数：房间ID、单位名称、阵营(byte)</summary>
    public event Action<string, string, byte>? OnUnitCreated;

    /// <summary>
    /// 战斗阶段变化事件（roomId, phase）。
    /// 由 BattleRoomEntity.BattlePhase SyncVar 变化触发。
    /// </summary>
    public event Action<string, BattlePhase>? BattlePhaseChanged;

    /// <summary>重连成功事件（客户端恢复连接后触发）</summary>
    public event Action<string>? OnReconnectSucceeded;

    // 本地玩家的 UnitController（在 OnPlayerEntityCreated 中查找并保存）
    private UnitController? _localController;

    /// <summary>上一次已知的战斗阶段值，用于检测 SyncVar 变化。</summary>
    private byte _lastKnownPhase;

    // ── Reconnect 清理 ──

    protected override void OnReconnectCleanup() {
        base.OnReconnectCleanup();
        _entityManager = null;
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    protected override void OnDisconnectCleanup() {
        base.OnDisconnectCleanup();
        _entityManager = null;
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    // ── Update ──

    protected override void OnAfterPollEvents() {
        _entityManager?.Update();

        // 检测 BattlePhase SyncVar 变化（LES 无公开 Changed 事件，通过轮询检测）
        if (_roomEntity != null) {
            var currentPhase = _roomEntity.BattlePhase.Value;
            if (currentPhase != _lastKnownPhase) {
                _lastKnownPhase = currentPhase;
                var phase = (BattlePhase)currentPhase;
                var roomId = _currentRoomId;
                if (roomId != null)
                    _pendingEventInvocations.Enqueue(() =>
                        BattlePhaseChanged?.Invoke(roomId, phase));
            }
        }
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
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    // ── IClientBattleService ──

    public GameRoom? GetRoom(string roomId) {
        List<UnitPawn> pawnsSnapshot;
        lock (_lock) {
            pawnsSnapshot = [.. _roomPawns];
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
        var roomId = _currentRoomId;
        if (roomId == null)
            return [];
        var room = GetRoom(roomId);
        return room != null ? [room] : [];
    }

    public GameRoom CreateRoom(string roomId) {
        _currentRoomId = roomId;
        var room = new GameRoom(roomId);
        lock (_lock) {
            _roomPawns.Clear();
        }
        return room;
    }

    public IUnitState CreateUnit(string roomId, string unitName, byte camp) {
        if (_roomEntity != null) {
            var req = new SyncCreateUnitRequest { UnitName = unitName, Camp = camp };
            _roomEntity.RequestCreateUnit(req);
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
        return _roomEntity?.IsFinished.Value ?? false;
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
        if (_roomEntity != null) {
            _roomEntity.RequestStartBattle();
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
        lock (_lock) {
            _roomEntity = entity;
            _currentRoomId = entity.RoomId.Value;
            _roomPawns.Clear();
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] Room entity created: {RoomId}", entity.RoomId.Value);
    }

    private void OnPawnEntityCreated(UnitPawn pawn) {
        var unitName = pawn.UnitName.Value;
        lock (_lock) {
            _roomPawns.Add(pawn);
        }

        // 订阅 UnitPawn 事件
        pawn.HealthChanged += (u, newHealth, oldHealth) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitHealthChanged?.Invoke(u.UnitName.Value, newHealth, oldHealth));
        pawn.UnitDied += (u) =>
            _pendingEventInvocations.Enqueue(() =>
                UnitDied?.Invoke(u.UnitName.Value));
        pawn.BuffAdded += (u, buff) => {
            var eventData = MapBuffData(buff);
            _pendingEventInvocations.Enqueue(() =>
                UnitBuffAdded?.Invoke(u.UnitName.Value, eventData));
        };
        pawn.BuffRemoved += (u, buff) => {
            var eventData = MapBuffData(buff);
            _pendingEventInvocations.Enqueue(() =>
                UnitBuffRemoved?.Invoke(u.UnitName.Value, eventData));
        };

        // 触发 OnUnitCreated 事件（通知 UI 层）
        var roomId = _currentRoomId;
        if (roomId != null) {
            _pendingEventInvocations.Enqueue(() =>
                OnUnitCreated?.Invoke(roomId, unitName, pawn.Camp.Value));
        }

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

    // ── IClientBattleService 兼容方法 ──

    void IClientBattleService.SubmitPlayerInput(float moveX, float moveY, byte skillFlags, float aimX, float aimY) {
        SubmitPlayerInput(
            new System.Numerics.Vector2(moveX, moveY),
            skillFlags,
            new System.Numerics.Vector2(aimX, aimY));
    }

    // ── 辅助 ──

    private static BuffEventData MapBuffData(SyncBuffData buff) => new() {
        BuffTypeId = buff.BuffTypeId,
        RemainingDuration = buff.RemainingDuration,
        StackCount = buff.StackCount,
        DamageType = buff.DamageType,
    };

    private UnitPawn? FindPawnByName(string unitName) {
        lock (_lock) {
            return _roomPawns.Find(p => p.UnitName.Value == unitName);
        }
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