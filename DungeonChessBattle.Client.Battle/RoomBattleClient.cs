using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Protocol.Enums;
using DungeonChessBattle.Entities;
using BattlePhase = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;
using BuffView = DungeonChessBattle.Battle.Domain.Combat.BuffView;
using DungeonChessBattle.Entities.SyncData;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Client.Battle.Diagnostics;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 房间战斗客户端，负责与房间端口的 LES 二进制协议 0xDC 通信。
/// 实现 IClientBattleService，管理 LES Entity：BattleRoomEntity、UnitPawn、PlayerRoomEntity。
/// 客户端同时只连接一个房间，使用单实例字段替代多房间 Dictionary。
/// 实体创建回调与模型构建见 RoomBattleClient.EntityMapping。
/// </summary>
public partial class RoomBattleClient(ILogger<RoomBattleClient> logger) : NetworkClientBase(logger), IClientBattleService {
    private ClientEntityManager? _entityManager;

    private const byte PacketHeader = 0xDC;

    // 单房间 Entity 缓存，P2-7 替代 Dictionary
    private BattleRoomEntity? _roomEntity;
    private readonly List<UnitPawn> _roomPawns = [];
    private string? _currentRoomId;
    private readonly Lock _lock = new();

    /// <summary>房间服务端权威创建时间，UTC Unix 秒；房间实体同步后回填。</summary>
    private long _roomCreatedUnix;

    /// <summary>单位生命值变化事件。参数：单位网络实体 ID、新生命值、旧生命值。</summary>
    public event Action<ushort, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位网络实体 ID。</summary>
    public event Action<ushort>? UnitDied;

    /// <summary>单位添加 Buff 事件。参数：单位网络实体 ID、Buff 数据。</summary>
    public event Action<ushort, BuffView>? UnitBuffAdded;

    /// <summary>单位移除 Buff 事件。参数：单位网络实体 ID、Buff 数据。</summary>
    public event Action<ushort, BuffView>? UnitBuffRemoved;

    /// <summary>单位创建事件。参数：房间 ID、单位网络实体 ID、单位名称、阵营字符串。</summary>
    public event Action<string, ushort, string, string>? OnUnitCreated;

    /// <summary>
    /// 战斗阶段变化事件，roomId 与 phase。
    /// 由 BattleRoomEntity.BattlePhase SyncVar 变化触发。
    /// </summary>
    public event Action<string, BattlePhase>? BattlePhaseChanged;

    // 本地玩家的 UnitController，在 OnUnitControllerCreated 回调中识别并缓存
    private UnitController? _localController;

    /// <summary>上一次已知的战斗阶段值，用于检测 SyncVar 变化。</summary>
    private BattlePhase _lastKnownPhase;

    // 传输统计，仅房间链路，主线程驱动，无并发
    private long _bytesIn;
    private int _packetsIn;
    private float _secondAccumulator;
    private int _packetsInPerSecond;
    private long _bytesInPerSecond;
    private int _packetsOutPerSecond;
    private long _bytesOutPerSecond;
    private CountingNetPeer? _countingPeer;

    /// <summary>重连时清理实体缓存与传输统计。</summary>
    protected override void OnReconnectCleanup() {
        base.OnReconnectCleanup();
        _entityManager = null;
        _localController = null;
        ResetTrafficCounters();
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
            _roomCreatedUnix = 0;
        }
    }

    /// <summary>
    /// 断开连接时清理实体管理器、房间缓存与传输统计。
    /// </summary>
    protected override void OnDisconnectCleanup() {
        base.OnDisconnectCleanup();
        _entityManager = null;
        _localController = null;
        ResetTrafficCounters();
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
            _roomCreatedUnix = 0;
        }
    }

    /// <summary>轮询网络事件后更新实体、结算每秒流量并检测战斗阶段变化。</summary>
    protected override void UpdateAfterPollEvents(float delta) {
        _entityManager?.Update();

        // 每秒流量统计结算，每秒一次，换算并重置累加器
        _secondAccumulator += delta;
        if (_secondAccumulator >= 1f) {
            _secondAccumulator -= 1f;
            _bytesInPerSecond = _bytesIn;
            _packetsInPerSecond = _packetsIn;
            _bytesOutPerSecond = _countingPeer?.BytesOut ?? 0;
            _packetsOutPerSecond = _countingPeer?.PacketsOut ?? 0;
            _bytesIn = 0;
            _packetsIn = 0;
            _countingPeer?.ResetTraffic();
        }

        // 检测 BattlePhase SyncVar 变化，LES 无公开 Changed 事件，通过轮询检测
        if (_roomEntity != null) {
            var currentPhase = _roomEntity.BattlePhase.Value;
            var phase = (BattlePhase)currentPhase;
            if (phase != _lastKnownPhase) {
                _lastKnownPhase = phase;
                var roomId = _currentRoomId;
                if (roomId != null)
                    BattlePhaseChanged?.Invoke(roomId, phase);
            }
        }
    }

    /// <summary>处理房间端口接收的 LES 二进制包，0xDC。</summary>
    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        _bytesIn += data.Length;
        _packetsIn++;
        if (data.Length > 0 && data[0] == PacketHeader) {
            _entityManager?.Deserialize(data);
        }
        // 房间端口不处理 JSON，其余丢弃
    }

    /// <summary>
    /// 连接建立时创建客户端实体管理器并订阅各实体类型的创建事件。
    /// 用计数装饰器包装 LES peer，采集出站流量。
    /// </summary>
    protected override void OnPeerConnectedInternal(NetPeer peer) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var countingPeer = new CountingNetPeer(lesPeer);
        _countingPeer = countingPeer;
        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ClientEntityManager(typesMap, countingPeer, PacketHeader);

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
            _roomCreatedUnix = 0;
        }
    }

    /// <summary>
    /// 获取房间服务端权威创建时间，UTC Unix 秒。
    /// 房间实体尚未同步时返回 0；调用方按需忽略。
    /// </summary>
    public long? GetRoomCreatedUnixTime(string roomId) {
        lock (_lock) {
            return _roomCreatedUnix;
        }
    }

    /// <summary>
    /// 向服务端发送创建单位 RPC 请求。单位实体由服务端创建并回传 Pawn，
    /// 客户端不维护本地状态。
    /// </summary>
    public void CreateUnit(string roomId, string unitName, string camp) {
        if (_roomEntity != null) {
            var req = new SyncCreateUnitRequest { UnitName = unitName, Camp = camp };
            _roomEntity.RequestCreateUnit(req);
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomBattleClient] CreateUnit: room entity not found for {RoomId}", roomId);
        }
    }

    /// <summary>
    /// 通过 RPC 向服务端发起施法读条，服务端权威结算。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="casterNetId">施法单位网络实体 ID。</param>
    /// <param name="targetNetId">目标单位网络实体 ID，范围伤害技能传 0。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <param name="targetPosX">位置目标 X，范围伤害技能使用。</param>
    /// <param name="targetPosZ">位置目标 Z，范围伤害技能使用。</param>
    public void CastSkill(string roomId, ushort casterNetId, ushort targetNetId, ushort skillId,
        float targetPosX = 0f, float targetPosZ = 0f) {
        if (_entityManager == null)
            return;

        var casterPawn = FindPawnById(casterNetId);
        if (casterPawn == null)
            return;

        var req = new SyncSkillRequest {
            CasterUnitNetId = casterNetId,
            TargetUnitNetId = targetNetId,
            SkillTypeId = skillId,
            TargetPosX = targetPosX,
            TargetPosZ = targetPosZ,
        };
        casterPawn.RequestCastSkill(req);
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

    /// <summary>IClientBattleService 接口的输入提交实现，float 参数版。</summary>
    void IClientBattleService.SubmitPlayerInput(float moveX, float moveY, byte skillFlags, float aimX, float aimY) {
        SubmitPlayerInput(
            new System.Numerics.Vector2(moveX, moveY),
            skillFlags,
            new System.Numerics.Vector2(aimX, aimY));
    }

    #region 网络状态统计

    /// <summary>当前实时延迟，毫秒，未连接时为 0。</summary>
    private int GetLatencyMs() => _serverPeer?.Ping ?? 0;

    /// <summary>获取传输层指标快照，延迟与每秒收发统计。</summary>
    public TransportMetrics TransportMetrics =>
        new(GetLatencyMs(), _packetsInPerSecond, _bytesInPerSecond, _packetsOutPerSecond, _bytesOutPerSecond);

    /// <summary>
    /// 获取 LES 实体同步指标；未连接或未进入战斗时返回 null。
    /// </summary>
    public BattleEntityMetrics? BattleEntityMetrics {
        get {
            var em = _entityManager;
            if (em == null)
                return null;
            return new BattleEntityMetrics(
                em.ServerTick, em.Tick, em.LastProcessedTick, em.StoredCommands,
                em.EntitiesCount, em.ServerInputBuffer, em.LerpBufferCount,
                em.LerpBufferTimeLength, em.NetworkJitter, em.PendingToRemoveEntites);
        }
    }

    /// <summary>
    /// 获取完整网络状态快照，对外唯一入口。
    /// </summary>
    public NetworkStatusSnapshot NetworkStatus {
        get {
            var peer = _serverPeer;
            string host = peer?.Address.ToString() ?? "";
            int port = peer?.Port ?? 0;
            return new NetworkStatusSnapshot(IsConnected, host, port, TransportMetrics, BattleEntityMetrics);
        }
    }

    /// <summary>清零传输统计，每秒结算由 UpdateAfterPollEvents 处理，此处仅全量清零。</summary>
    private void ResetTrafficCounters() {
        _bytesIn = 0;
        _packetsIn = 0;
        _secondAccumulator = 0;
        _packetsInPerSecond = 0;
        _bytesInPerSecond = 0;
        _packetsOutPerSecond = 0;
        _bytesOutPerSecond = 0;
        _countingPeer = null;
    }

    #endregion
}
