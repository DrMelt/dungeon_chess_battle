using LiteNetLib;
using LiteNetLib.Utils;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.Requests;
using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Buffs;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.Battle.Shared.Buffs;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.GameConfig;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Client.Battle.Diagnostics;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 房间战斗客户端，负责与房间端口的 LES 二进制协议 0xDC 通信。
/// 实现 IClientBattleService 与 IBattleViewSource，管理 LES Entity：BattleRoomEntity、UnitPawn、UnitController。
/// 在线端构建 BattleScene（领域单位 BattleUnit 直接实现展示契约），每帧把 UnitPawn SyncVar 回填到领域，
/// 客户端不做领域推导与移动预测；作为 IBattleViewSource 供 UI 取数。
/// 实体创建回调与模型构建见 RoomBattleClient.EntityMapping。
/// </summary>
public partial class RoomBattleClient(ILogger<RoomBattleClient> logger) : NetworkClientBase(logger), IClientBattleService, IBattleViewSource {
    private ClientEntityManager? _entityManager;

    /// <summary>把服务器截止 tick 换算为剩余秒数；实体管理器未就绪时返回 0。</summary>
    private float EndTickToRemaining(ushort tick) =>
        _entityManager is { } em ? SyncTickHelper.RemainingSeconds(em, tick) : 0f;

    private BattleRoomEntity? _roomEntity;
    private readonly List<UnitPawn> _roomPawns = [];
    private string? _currentRoomId;
    private readonly Lock _lock = new();

    /// <summary>单位生命值变化事件。参数：单位网络实体 ID、新生命值、旧生命值。</summary>
    public event Action<ushort, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位网络实体 ID。</summary>
    public event Action<ushort>? UnitDied;

    /// <summary>单位聚焦目标变化事件。参数：单位网络实体 ID、目标单位网络实体 ID，0 表示无聚焦目标。</summary>
    public event Action<ushort, ushort>? UnitFocusTargetChanged;

    /// <summary>单位创建事件。参数：房间 ID、单位网络实体 ID、单位名称、阵营列表。</summary>
    public event Action<string, ushort, string, IReadOnlyList<string>>? OnUnitCreated;

    /// <summary>
    /// 战斗阶段变化事件，roomId 与 phase。
    /// 直读 BattleRoomEntity SyncVar 轮询检测到阶段变化时触发。
    /// </summary>
    public event Action<string, BattlePhase>? BattlePhaseChanged;

    /// <summary>战斗事件日志事件。参数：房间 ID、本帧领域事件列表。</summary>
    public event Action<string, IReadOnlyList<IBattleEvent>>? BattleEventsReceived;

    // 本地玩家的 UnitController，在 OnUnitControllerCreated 回调中识别并缓存
    private UnitController? _localController;

    /// <summary>上一次已知的战斗阶段值，用于检测 SyncVar 变化。</summary>
    private BattlePhase _lastKnownPhase = BattlePhase.Waiting;

    // 传输统计，仅房间链路，主线程驱动，无并发
    private long _bytesIn;
    private int _packetsIn;
    private float _secondAccumulator;
    private int _packetsInPerSecond;
    private long _bytesInPerSecond;
    private int _packetsOutPerSecond;
    private long _bytesOutPerSecond;
    private CountingNetPeer? _countingPeer;

    /// <summary>在线端战斗世界：领域单位 BattleUnit 作为展示源，仅作容器与回填落点，不推进领域逻辑。</summary>
    private BattleScene? _battleScene;

    /// <summary>网络实体 ID 到领域单位映射，定位回填目标。</summary>
    private readonly Dictionary<ushort, BattleUnit> _battleUnitByNetId = [];

    /// <summary>单位网络 ID → 聚焦目标网络 ID，0 表示无聚焦目标。</summary>
    private readonly Dictionary<ushort, ushort> _focusByNetId = [];

    /// <summary>本地玩家单位网络 ID，控制器就绪后写入，0 表示未就绪。</summary>
    private ushort _localNetId;

    /// <summary>清理房间会话本地状态：实体缓存、战斗世界、阶段检测基准与传输统计。</summary>
    private void ClearRoomSessionState() {
        _entityManager = null;
        _localController = null;
        _battleScene = null;
        _battleUnitByNetId.Clear();
        _focusByNetId.Clear();
        _localNetId = 0;
        _eventLog.Clear();
        _lastKnownPhase = BattlePhase.Waiting;
        ResetTrafficCounters();
        lock (_lock) {
            _roomEntity = null;
            _roomPawns.Clear();
            _currentRoomId = null;
        }
    }

    /// <summary>重连时清理房间会话状态。</summary>
    protected override void OnReconnectCleanup() {
        base.OnReconnectCleanup();
        ClearRoomSessionState();
    }

    /// <summary>断开连接时清理房间会话状态。</summary>
    protected override void OnDisconnectCleanup() {
        base.OnDisconnectCleanup();
        ClearRoomSessionState();
    }

    /// <summary>轮询网络事件后更新实体、回填领域单位、结算每秒流量并检测战斗阶段变化。</summary>
    protected override void UpdateAfterPollEvents(float delta) {
        // 副本键同步后构建战斗世界；未同步时为空操作，随下一帧重试
        EnsureBattleScene();
        _entityManager?.Update();

        // 实体更新后把 UnitPawn SyncVar 回填到领域单位 BattleUnit，供 UI 经 IBattleViewSource 只读取数
        SyncBattleUnits();

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

        // 检测 BattlePhase 投影变化，LES 无公开 Changed 事件，通过轮询检测
        if (_roomEntity is { } room) {
            var phase = (BattlePhase)room.BattlePhase.Value;
            if (phase != _lastKnownPhase) {
                _lastKnownPhase = phase;
                var roomId = _currentRoomId;
                if (roomId != null)
                    BattlePhaseChanged?.Invoke(roomId, phase);
            }
        }
    }

    /// <summary>
    /// 副本键同步后就绪时构建在线战斗世界：领域单位 BattleUnit 作为展示源。
    /// 副本键未同步返回并随下一帧重试；构建时补注册已到达的单位。
    /// </summary>
    private void EnsureBattleScene() {
        if (_battleScene != null)
            return;
        if (_roomEntity is not { } room || string.IsNullOrWhiteSpace(room.DungeonKey.Value))
            return;

        var dungeonKey = room.DungeonKey.Value;
        _battleScene = new BattleScene(
            DungeonRegistry.Instance.GetRelations(dungeonKey),
            new PhysicsMovementScene(DungeonRegistry.Instance.GetMovementLayout(dungeonKey)));
        foreach (var unit in _battleUnitByNetId.Values)
            _battleScene.AddUnit(unit);
    }

    /// <summary>Pawn 创建时构建领域单位并注册；战斗世界未就绪时先存映射，构建时统一补注册。</summary>
    private void AddPawnUnit(UnitPawn pawn) {
        if (_battleUnitByNetId.ContainsKey(pawn.Id))
            return;

        var unit = new BattleUnit {
            UnitNetId = pawn.Id,
            UnitName = pawn.UnitName.Value,
            Camps = pawn.CampTags,
            Skills = pawn.Skills,
        };
        _battleUnitByNetId[pawn.Id] = unit;
        _battleScene?.AddUnit(unit);
    }

    /// <summary>逐 UnitPawn 回填对应领域单位：展示状态以服务端 SyncVar 为准。</summary>
    private void SyncBattleUnits() {
        foreach (var pawn in _roomPawns)
            SyncUnit(pawn);
    }

    /// <summary>把单个 UnitPawn 的状态回填到领域 BattleUnit；缺失单位说明尚未创建，跳过。</summary>
    private void SyncUnit(UnitPawn pawn) {
        if (!_battleUnitByNetId.TryGetValue(pawn.Id, out var unit))
            return;

        unit.Position = pawn.Position.Value;
        unit.Direction = pawn.Direction.Value;
        unit.Health = pawn.Health.Value;
        unit.MaxHealth = pawn.MaxHealth.Value;
        unit.BodyRadius = pawn.BodyRadius.Value;
        string casting = pawn.SkillCasting.Value;
        unit.SkillCasting = string.IsNullOrEmpty(casting) ? default : new SkillKeyId(casting);
        unit.SkillCastRemaining = pawn.SkillCastRemaining.Value;
        unit.GcdRemaining = EndTickToRemaining(pawn.GcdEndServerTick.Value);
        SyncBuffs(unit, pawn);
        SyncCooldowns(unit, pawn.SkillCooldowns.Value);
        _focusByNetId[pawn.Id] = pawn.FocusTargetNetId.Value;
    }

    /// <summary>回填 Buff 展示：从网络数据重建运行时 Buff 壳，在线端不推进效果，仅承载展示字段。</summary>
    private void SyncBuffs(BattleUnit unit, UnitPawn pawn) {
        var list = unit.RuntimeState.Buffs;
        list.Clear();
        foreach (var b in pawn.BuffsList) {
            var instance = new BuffInstance {
                BuffTypeId = b.BuffTypeId,
                TargetNetId = unit.UnitNetId,
                FromNetId = b.SourceUnitNetId,
                MaxStacks = Math.Max(1, (int)b.MaxStackCount),
                Remaining = EndTickToRemaining(b.EndServerTick),
                Stacks = b.StackCount,
                DamageType = (DamageType)b.DamageType,
            };
            list.Add(new ActiveBuff(instance, NetworkBuffDefinition.Instance, NoOpBuffEffect.Instance));
        }
    }

    /// <summary>回填技能冷却展示：从网络整包还原个体冷却条目。</summary>
    private void SyncCooldowns(BattleUnit unit, SyncSkillCooldownSnapshot? snapshot) {
        var list = unit.RuntimeState.Cooldowns;
        list.Clear();
        if (snapshot == null)
            return;
        foreach (var entry in snapshot.Entries)
            list.Add(new CooldownEntry(new SkillKeyId(entry.SkillId), EndTickToRemaining(entry.EndServerTick)));
    }

    /// <inheritdoc />
    public IReadOnlyList<IUnitUiView> Units => _battleScene?.BattleUnits ?? [];

    /// <inheritdoc />
    public IUnitUiView? FindUnit(ushort netId) => _battleScene?.FindUnit(netId) as IUnitUiView;

    /// <summary>按网络 ID 查询施法判定视图（权威位置），不存在返回 null。</summary>
    public ISkillCasterView? FindCaster(ushort netId) => _battleUnitByNetId.GetValueOrDefault(netId) as ISkillCasterView;

    /// <summary>本地玩家单位展示视图，控制器未就绪返回 null。</summary>
    public IUnitUiView? LocalUnit => FindUnit(_localNetId);

    /// <summary>本地玩家单位施法判定视图（权威位置），控制器未就绪返回 null。</summary>
    public ISkillCasterView? LocalCaster => FindCaster(_localNetId);

    /// <summary>本地玩家聚焦目标单位展示视图，焦点为 0 或无目标返回 null。</summary>
    public IUnitUiView? LocalFocus {
        get {
            ushort target = _focusByNetId.GetValueOrDefault(_localNetId);
            return target == 0 ? null : FindUnit(target);
        }
    }

    /// <summary>处理房间端口接收的二进制包：先识别可靠消息帧，其余 0xDC 帧交 LES 反序列化。</summary>
    protected override void OnNetworkReceiveInternal(ReadOnlySpan<byte> data) {
        _bytesIn += data.Length;
        _packetsIn++;
        if (ReliableMessageFrame.TryReadBody(data, out var body)) {
            HandleReliableServerMessage(body);
            return;
        }
        if (data.Length > 0 && data[0] == NetworkDefaults.PacketHeader)
            _entityManager?.Deserialize(data);
    }

    /// <summary>
    /// 处理服务器可靠消息：解码整帧战斗事件日志并触发 <see cref="BattleEventsReceived"/>。
    /// 消息体已由 <see cref="ReliableMessageFrame"/> 从帧中提取；连接内可靠有序，断线重连期间的事件不补发。
    /// </summary>
    private void HandleReliableServerMessage(NetDataReader body) {
        if (_roomEntity == null)
            return;
        ReliableBattleEventLog log;
        try {
            log = new ReliableBattleEventLog();
            log.Deserialize(body);
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentOutOfRangeException) {
            _logger.LogWarning("Discard malformed reliable battle event log: {Reason}", ex.Message);
            return;
        }
        if (log.Events is not { Length: > 0 })
            return;
        var decoded = new List<IBattleEvent>(log.Events.Length);
        foreach (var e in log.Events) {
            if (BattleEventCoder.Decode(e) is { } domainEvent)
                decoded.Add(domainEvent);
        }
        var roomId = _currentRoomId;
        if (roomId != null) {
            _eventLog.Append(decoded, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            BattleEventsReceived?.Invoke(roomId, decoded);
        }
    }

    /// <summary>
    /// 连接建立时创建客户端实体管理器并订阅各实体类型的创建事件。
    /// 用计数装饰器包装 LES peer，采集出站流量。
    /// </summary>
    protected override void OnPeerConnectedInternal(NetPeer peer) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var countingPeer = new CountingNetPeer(lesPeer);
        _countingPeer = countingPeer;
        var typesMap = EntityTypesRegistry.EntityTypesMap;
        _entityManager = new ClientEntityManager(typesMap, countingPeer, NetworkDefaults.PacketHeader);

        // 订阅所有同步 Entity 类型的创建事件
        _entityManager.GetEntities<BattleRoomEntity>()
            .SubscribeToConstructed(OnRoomEntityCreated, callOnExisting: true);
        _entityManager.GetEntities<UnitPawn>()
            .SubscribeToConstructed(OnPawnEntityCreated, callOnExisting: true);
        _entityManager.GetEntities<UnitController>()
            .SubscribeToConstructed(OnUnitControllerCreated, callOnExisting: true);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("LES EntityManager created for peer {PeerId}", peer.Id);
    }

    /// <summary>对端断开时清理房间会话状态。</summary>
    /// <param name="peer">断开的对端。</param>
    /// <param name="info">断开信息。</param>
    protected override void OnPeerDisconnectedInternal(NetPeer peer, DisconnectInfo info) {
        base.OnPeerDisconnectedInternal(peer, info);
        ClearRoomSessionState();
    }

    /// <summary>
    /// 战斗开始时间（服务端权威，UTC Unix 秒），直接读取 BattleRoomEntity 同步值。
    /// 房间实体尚未同步时返回 0；Running 阶段调用时实体必然已同步。
    /// </summary>
    public long? BattleStartUnixTime => _roomEntity?.BattleStartUnixTime.Value;

    /// <summary>
    /// 经可靠请求通道向服务端发起施法读条，服务端权威校验与结算。
    /// 施法者由服务端从请求来源控制器携带的单位推导，不接收客户端指定。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="casterNetId">施法单位网络实体 ID（仅作接口兼容，施法者以控制器为准）。</param>
    /// <param name="targetNetId">目标单位网络实体 ID，范围伤害技能传 0。</param>
    /// <param name="skillId">技能配置键。</param>
    /// <param name="targetPosX">位置目标 X，范围伤害技能使用。</param>
    /// <param name="targetPosZ">位置目标 Z，范围伤害技能使用。</param>
    public void CastSkill(string roomId, ushort casterNetId, ushort targetNetId, string skillId,
        float targetPosX = 0f, float targetPosZ = 0f) {
        var controller = _localController;
        if (controller == null)
            return;

        var req = new CastSkillRequest {
            SkillTypeId = skillId,
            TargetNetId = targetNetId,
            TargetPosX = targetPosX,
            TargetPosZ = targetPosZ,
        };
        controller.SendCastSkillRequest(req, onResult => {
            if (!onResult && _logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("CastSkill rejected by server: skill={SkillId}, target={TargetId}",
                    skillId, targetNetId);
        });
    }

    /// <summary>判断当前房间战斗是否已结束。</summary>
    public bool CheckBattleEnded(string roomId) {
        return _roomEntity?.IsFinished.Value ?? false;
    }

    /// <summary>
    /// 经可靠请求通道请求设置单位聚焦目标，服务端校验并写回权威状态。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitNetId">设置聚焦目标的单位网络实体 ID（仅作接口兼容，目标以控制器为准）。</param>
    /// <param name="targetNetId">目标单位网络实体 ID，传 0 表示清除聚焦目标。</param>
    public void SetFocusTarget(string roomId, ushort unitNetId, ushort targetNetId) {
        var controller = _localController;
        if (controller == null)
            return;

        controller.SendSetFocusTargetRequest(new SetFocusTargetRequest { TargetUnitNetId = targetNetId }, onResult => {
            if (!onResult && _logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("SetFocusTarget rejected by server: target={TargetId}", targetNetId);
        });

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("SetFocusTarget: unit={UnitId} -> target={TargetId}", unitNetId, targetNetId);
    }

    /// <summary>Godot UI 层调用，提交当前帧的移动输入到 LES 框架。
    /// 框架自动进行 Delta 编码、UDP 发送、预测回滚。
    /// </summary>
    public void SubmitPlayerInput(System.Numerics.Vector2 moveDir) {
        if (_localController != null) {
            _localController.SubmitInput(moveDir);
            if (_logger.IsEnabled(LogLevel.Trace))
                _logger.LogTrace("Input submitted: dir={MoveDir}", moveDir);
        }
        else if (_logger.IsEnabled(LogLevel.Warning) && moveDir != System.Numerics.Vector2.Zero) {
            _logger.LogWarning("Local controller not ready, input dropped: dir={MoveDir}", moveDir);
        }
    }

    /// <summary>IClientBattleService 接口的输入提交实现，float 参数版。</summary>
    void IClientBattleService.SubmitPlayerInput(float moveX, float moveY) {
        SubmitPlayerInput(new System.Numerics.Vector2(moveX, moveY));
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
