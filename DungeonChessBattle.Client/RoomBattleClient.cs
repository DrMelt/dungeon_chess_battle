using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Client;

/// <summary>
/// 房间战斗客户端，负责与房间端口的 LES 二进制协议 (0xDC) 通信。
/// 实现 IClientBattleService，管理 LES Entity（BattleRoomEntity、UnitPawn、PlayerRoomEntity）。
/// 客户端同时只连接一个房间，使用单实例字段替代多房间 Dictionary。
/// 实体创建回调与模型构建见 RoomBattleClient.EntityMapping。
/// </summary>
public partial class RoomBattleClient(ILogger<RoomBattleClient> logger) : NetworkClientBase(logger), IClientBattleService {
    private ClientEntityManager? _entityManager;

    private const byte PacketHeader = 0xDC;

    // 单房间 Entity 缓存（P2-7：替代 Dictionary）
    private BattleRoomEntity? _roomEntity;
    private readonly List<UnitPawn> _roomPawns = [];
    private string? _currentRoomId;
    private readonly Lock _lock = new();

    /// <summary>单位生命值变化事件。参数：单位名称、新生命值、旧生命值。</summary>
    public event Action<string, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位名称。</summary>
    public event Action<string>? UnitDied;

    /// <summary>单位添加 Buff 事件。参数：单位名称、Buff 数据。</summary>
    public event Action<string, BuffEventData>? UnitBuffAdded;

    /// <summary>单位移除 Buff 事件。参数：单位名称、Buff 数据。</summary>
    public event Action<string, BuffEventData>? UnitBuffRemoved;

    /// <summary>单位创建事件。参数：房间ID、单位名称、阵营(字符串)</summary>
    public event Action<string, string, string>? OnUnitCreated;

    /// <summary>
    /// 战斗阶段变化事件（roomId, phase）。
    /// 由 BattleRoomEntity.BattlePhase SyncVar 变化触发。
    /// </summary>
    public event Action<string, BattlePhase>? BattlePhaseChanged;

    // 本地玩家的 UnitController（在 OnUnitControllerCreated 回调中识别并缓存）
    private UnitController? _localController;

    /// <summary>上一次已知的战斗阶段值，用于检测 SyncVar 变化。</summary>
    private byte _lastKnownPhase;

    /// <summary>重连时清理实体缓存。</summary>
    protected override void OnReconnectCleanup() {
        base.OnReconnectCleanup();
        _entityManager = null;
        _localController = null;
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    /// <summary>
    /// 断开连接时清理实体管理器与房间缓存。
    /// </summary>
    protected override void OnDisconnectCleanup() {
        base.OnDisconnectCleanup();
        _entityManager = null;
        _localController = null;
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    /// <summary>轮询网络事件后更新实体并检测战斗阶段变化。</summary>
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

    /// <summary>处理房间端口接收的 LES 二进制包（0xDC）。</summary>
    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        if (data.Length > 0 && data[0] == PacketHeader) {
            _entityManager?.Deserialize(data);
        }
        // 房间端口不处理 JSON，其余丢弃
    }

    /// <summary>
    /// 连接建立时创建客户端实体管理器并订阅各实体类型的创建事件。
    /// </summary>
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
        _entityManager.GetEntities<UnitController>()
            .SubscribeToConstructed(OnUnitControllerCreated, callOnExisting: true);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomBattleClient] LES EntityManager created for peer {PeerId}", peer.Id);
    }

    /// <summary>
    /// 对端断开时清理实体管理器与房间缓存。
    /// </summary>
    /// <param name="peer">断开的对端。</param>
    /// <param name="info">断开信息。</param>
    protected override void OnPeerDisconnectedInternal(NetPeer peer, DisconnectInfo info) {
        _entityManager = null;
        _localController = null;
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    /// <summary>
    /// 按房间 ID 组装房间数据（从本地缓存的 Pawn 实体构建）。
    /// </summary>
    public GameRoom? GetRoom(string roomId) {
        List<UnitPawn> pawnsSnapshot;
        lock (_lock) {
            pawnsSnapshot = [.. _roomPawns];
        }
        var room = new GameRoom(roomId);
        foreach (var p in pawnsSnapshot) {
            var model = BuildModelFromPawn(p);
            if (p.Camp.Value == CampConstants.CampA)
                room.UnitsA.Add(model);
            else if (p.Camp.Value == CampConstants.CampB)
                room.UnitsB.Add(model);
        }
        return room;
    }

    /// <summary>获取当前房间（客户端仅持有单房间）。</summary>
    public IEnumerable<GameRoom> GetAllRooms() {
        var roomId = _currentRoomId;
        if (roomId == null)
            return [];
        var room = GetRoom(roomId);
        return room != null ? [room] : [];
    }

    /// <summary>
    /// 创建房间记录并清空本地 Pawn 缓存。
    /// </summary>
    public GameRoom CreateRoom(string roomId) {
        _currentRoomId = roomId;
        var room = new GameRoom(roomId);
        lock (_lock) {
            _roomPawns.Clear();
        }
        return room;
    }

    /// <summary>
    /// 向服务端发送创建单位 RPC 请求，并返回本地临时单位模型。
    /// </summary>
    public IUnitState CreateUnit(string roomId, string unitName, string camp) {
        if (_roomEntity != null) {
            var req = new SyncCreateUnitRequest { UnitName = unitName, Camp = camp };
            _roomEntity.RequestCreateUnit(req);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomBattleClient] CreateUnit: room entity not found for {RoomId}", roomId);
        }

        var model = new UnitModel { UnitStateName = unitName, Camps = [camp] };
        return model;
    }

    /// <summary>
    /// 通过 RPC 向服务端施放技能。
    /// </summary>
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

    /// <summary>
    /// 客户端不对 Buff 做本地结算（服务端权威），空实现。
    /// </summary>
    public void UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime) {
        // Buff 结算由服务端权威执行，客户端仅接收同步更新
    }

    /// <summary>判断当前房间战斗是否已结束。</summary>
    public bool CheckBattleEnded(string roomId) {
        return _roomEntity?.IsFinished.Value ?? false;
    }

    /// <summary>
    /// Godot UI 层调用，提交当前帧的玩家输入到 LES 框架。
    /// 框架自动进行 Delta 编码、UDP 发送、预测回滚。
    /// </summary>
    public void SubmitPlayerInput(System.Numerics.Vector2 moveDir, byte skillFlags, System.Numerics.Vector2 aimPos) {
        if (_localController != null) {
            _localController.SubmitInput(moveDir, skillFlags, aimPos);
            if (moveDir != System.Numerics.Vector2.Zero) {
                _logger.LogInformation("[RoomBattleClient] Input submitted: dir={MoveDir}",
                    moveDir);
            }
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[RoomBattleClient] Input submitted: dir={MoveDir}, flags={SkillFlags}, aim={AimPos}",
                    moveDir, skillFlags, aimPos);
        }
        else if (_logger.IsEnabled(LogLevel.Warning) && (moveDir != System.Numerics.Vector2.Zero || skillFlags != 0)) {
            _logger.LogWarning("[RoomBattleClient] Local controller not ready, input dropped: dir={MoveDir}, flags={SkillFlags}",
                moveDir, skillFlags);
        }
    }

    /// <summary>通过 RPC 请求开始战斗。</summary>
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

    /// <summary>IClientBattleService 接口的输入提交实现（float 参数版）。</summary>
    void IClientBattleService.SubmitPlayerInput(float moveX, float moveY, byte skillFlags, float aimX, float aimY) {
        SubmitPlayerInput(
            new System.Numerics.Vector2(moveX, moveY),
            skillFlags,
            new System.Numerics.Vector2(aimX, aimY));
    }
}
