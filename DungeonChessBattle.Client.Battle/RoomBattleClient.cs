using LiteNetLib;
using LiteNetLib.Utils;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.Battle.Entities.Requests;
using DungeonChessBattle.Battle.Entities.SyncData;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.GameConfig;
using Microsoft.Extensions.Logging;
using DungeonChessBattle.Client.Battle.Diagnostics;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 房间战斗客户端，负责与房间端口的 LES 二进制协议 0xDC 通信。
/// 实现 IClientBattleService 与 IBattleViewSource，管理 LES Entity：BattleRoomEntity、UnitPawn、UnitController。
/// 在线端构建 BattleScene（领域单位 BattleUnit 实现展示契约），由 <see cref="ClientBattleLoop"/> 每渲染帧
/// 把网络 SyncVar 读数回填进领域单位；当前在线端不跑本地结算，移动与伤害一律服务端权威；
/// 房间同步状态经 <see cref="BattleRoomState"/> 投影统一读取，作为 IBattleViewSource 供 UI 取数。
/// 实体创建回调与模型构建见 RoomBattleClient.EntityMapping。
/// </summary>
public partial class RoomBattleClient(ILogger<RoomBattleClient> logger) : NetworkClientBase(logger), IClientBattleService, IBattleViewSource {
    /// <summary>
    /// LES 两级缓冲目标水位下界，秒。<c>PreloadNextState</c> 按 <c>NetworkJitter × 1.5</c> 加本值
    /// 同时约束下行插值缓冲与服务端输入队列，水位除以 tick 宽度即 <c>TickLag</c> 的 debt 与 queue 两段。
    /// 框架默认 0.025 在 128 Hz 下折成 3.2 tick。
    /// </summary>
    private const float BufferLowestSeconds = 0.002f;

    /// <summary>
    /// LES 两级缓冲目标水位上界，秒。与下界的张开幅度须大于 <c>TimeSpeedChangeCoef</c> 的 ±10% 调节量，
    /// 否则客户端 tick 节拍在区间两端反复摆动。框架默认 0.05。
    /// </summary>
    private const float BufferHighestSeconds = 0.006f;

    private ClientEntityManager? _entityManager;

    private BattleRoomEntity? _roomEntity;
    private readonly List<UnitPawn> _roomPawns = [];
    private string? _currentRoomId;
    private readonly Lock _lock = new();

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

    // 下行状态流健康：A/B tick 差每帧采样、每秒结算，用于抓状态跳号
    private int _spreadSamples;
    private int _spreadSum;
    private int _spreadMax;
    private float _stateSpreadAvgPerSecond;
    private int _stateSpreadMaxPerSecond;

    /// <summary>在线端战斗世界：领域单位 BattleUnit 作为展示源，状态由 SyncVar 回填。</summary>
    private BattleScene? _battleScene;

    /// <summary>网络实体 ID 到领域单位映射，展示回填与取数定位。</summary>
    private readonly Dictionary<ushort, BattleUnit> _battleUnitByNetId = [];

    /// <summary>网络实体 ID 到 UnitPawn 映射，SyncVar 读数回填的定位载体。</summary>
    private readonly Dictionary<ushort, UnitPawn> _pawnByNetId = [];

    /// <summary>在线客户端战斗世界，展示契约的领域单位容器；构建前为 null。</summary>
    internal BattleScene? BattleScene => _battleScene;

    /// <summary>网络实体 ID 到 UnitPawn 的映射，供展示回填定位载体。</summary>
    internal IReadOnlyDictionary<ushort, UnitPawn> PawnByNetId => _pawnByNetId;

    /// <summary>本地玩家控制器，用于注入移动输入；未就绪为 null。</summary>
    internal UnitController? LocalController => _localController;

    /// <summary>当前房间同步状态投影，来自服务端权威 BattleRoomEntity；未同步时为默认。</summary>
    internal BattleRoomState RoomState => _roomEntity is { } room
        ? new BattleRoomState(room.RoomId.Value, room.DungeonKey.Value,
            (BattlePhase)room.BattlePhase.Value, room.BattleStartUnixTime.Value)
        : default;

    /// <summary>房间当前阶段（来自服务端同步 BattleRoomEntity），未同步时为 Waiting。</summary>
    internal BattlePhase RoomPhase => RoomState.Phase;

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
        _pawnByNetId.Clear();
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

    /// <summary>轮询网络事件后驱动实体同步与展示回填，结算每秒流量并检测战斗阶段变化。</summary>
    protected override void UpdateAfterPollEvents(float delta) {
        // 副本键同步后构建战斗世界；未同步时为空操作，随下一帧重试
        EnsureBattleScene();
        _entityManager?.Update();
        SampleStateSpread();

        // 展示取数由 ClientBattleLoop（LocalSingleton）在 LES 主循环内驱动：
        // VisualUpdate 每渲染帧回填 SyncVar 读数进本地 BattleScene，展示再经 IBattleViewSource 直读。

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
            _stateSpreadAvgPerSecond = _spreadSamples > 0 ? (float)_spreadSum / _spreadSamples : 0f;
            _stateSpreadMaxPerSecond = _spreadMax;
            _spreadSamples = 0;
            _spreadSum = 0;
            _spreadMax = 0;
            _countingPeer?.ResetTraffic();
        }

        // 聚焦目标展示以服务端权威 SyncVar 为准，本地不参与推算。
        SyncFocusTargets();

        // 检测 BattlePhase 投影变化，LES 无公开 Changed 事件，通过轮询检测
        var phase = RoomState.Phase;
        if (phase != _lastKnownPhase) {
            _lastKnownPhase = phase;
            var roomId = _currentRoomId;
            if (roomId != null)
                BattlePhaseChanged?.Invoke(roomId, phase);
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

    /// <summary>Pawn 创建时构建领域单位并注册；战斗世界未就绪时先存映射，构建时统一补注册。
    /// 装配完整单位配置（数值、AI、仇恨），使本地 BattleScene 能确定性重跑。</summary>
    private void AddPawnUnit(UnitPawn pawn) {
        if (_battleUnitByNetId.ContainsKey(pawn.Id))
            return;

        var config = UnitRegistry.Instance.GetByKey(pawn.UnitKeyName.Value);
        var unit = new BattleUnit {
            UnitId = pawn.Id,
            UnitName = pawn.UnitKeyName.Value,
            Camps = pawn.CampTags,
            Skills = config?.Skills ?? pawn.Skills,
            Intelligence = config?.Intelligence,
            HateRule = config?.HateRule,
            HateFactor = config?.HateFactor ?? 1f,
            MaxHealth = config?.MaxHealth ?? 0f,
            Health = config?.MaxHealth ?? 0f,
            PhysicalAttackBase = config?.PhysicalAttackBase ?? 1f,
            PhysicalTakePercent = config?.PhysicalTakePercent ?? 1f,
            MagicAttackBase = config?.MagicAttackBase ?? 1f,
            MagicTakePercent = config?.MagicTakePercent ?? 1f,
            CureIntensity = config?.CureIntensity ?? 1f,
            BaseSpeed = config?.BaseSpeed ?? pawn.BaseSpeed.Value,
            BodyRadius = config?.BodyRadius ?? pawn.BodyRadius.Value,
            Position = pawn.Position.Value,
        };
        _battleUnitByNetId[pawn.Id] = unit;
        _pawnByNetId[pawn.Id] = pawn;
        _battleScene?.AddUnit(unit);
    }

    /// <summary>轮询聚焦目标 SyncVar：本地模拟不写聚焦，聚焦一律取服务端权威值；服务端保证目标存活。</summary>
    private void SyncFocusTargets() {
        foreach (var pawn in _roomPawns)
            _focusByNetId[pawn.Id] = pawn.FocusTargetNetId.Value;
    }

    /// <inheritdoc />
    public IReadOnlyList<IUnitUiView> Units => _battleScene?.BattleUnits ?? [];

    /// <inheritdoc />
    public IUnitUiView? FindUnit(ushort netId) => _battleScene?.FindUnit(netId) as IUnitUiView;

    /// <summary>按网络 ID 查询施法判定视图（本地结算位置），不存在返回 null。</summary>
    public ISkillCasterView? FindCaster(ushort netId) => _battleUnitByNetId.GetValueOrDefault(netId);

    /// <summary>本地玩家单位展示视图，控制器未就绪返回 null。</summary>
    public IUnitUiView? LocalUnit => FindUnit(_localNetId);

    /// <summary>本地玩家单位施法判定视图（本地结算位置），控制器未就绪返回 null。</summary>
    public ISkillCasterView? LocalCaster => FindCaster(_localNetId);

    /// <summary>本地玩家聚焦目标单位展示视图，无聚焦目标或目标已被服务端清 0 返回 null。</summary>
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
        _entityManager = new ClientEntityManager(typesMap, countingPeer, NetworkDefaults.PacketHeader) {
            // 重设两级缓冲水位：默认值在 128 Hz 下折成 3.2/6.4 tick，本地回环的 TickLag 几乎全由此撑起。
            // 该水位只够本地链路，公网部署需按 RTT 与抖动分档，否则插值饥饿。
            PreferredBufferTimeLowest = BufferLowestSeconds,
            PreferredBufferTimeHighest = BufferHighestSeconds
        };

        // 订阅所有同步 Entity 类型的创建事件
        _entityManager.GetEntities<BattleRoomEntity>()
            .SubscribeToConstructed(OnRoomEntityCreated, callOnExisting: true);
        _entityManager.GetEntities<UnitPawn>()
            .SubscribeToConstructed(OnPawnEntityCreated, callOnExisting: true);
        _entityManager.GetEntities<UnitController>()
            .SubscribeToConstructed(OnUnitControllerCreated, callOnExisting: true);

        // 展示取数：ClientBattleLoop 的 VisualUpdate 每渲染帧把 SyncVar 读数回填进本地 BattleScene。
        _entityManager.AddLocalSingleton(new ClientBattleLoop(this));

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
    /// 战斗开始时间（服务端权威，UTC Unix 秒），经房间同步状态投影读取。
    /// 房间实体尚未同步时返回 null；Running 阶段调用时实体必然已同步。
    /// </summary>
    public long? BattleStartUnixTime => RoomState.BattleStartUnixTime;

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
        return RoomState.IsFinished;
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

    /// <summary>获取传输层指标快照：往返与单向延迟、每秒收发、累计丢包率、本端出站可靠队列积压。</summary>
    public TransportMetrics TransportMetrics {
        get {
            var peer = _serverPeer;
            return new TransportMetrics(
                peer?.RoundTripTime ?? 0, peer?.Ping ?? 0,
                _packetsInPerSecond, _bytesInPerSecond, _packetsOutPerSecond, _bytesOutPerSecond,
                peer?.Statistics.PacketLossPercent ?? 0,
                peer?.GetPacketsCountInReliableQueue(true) ?? 0);
        }
    }

    /// <summary>
    /// 获取 LES 实体同步的原始读数；未连接或未进入战斗时返回 null。
    /// 只搬运 <c>ClientEntityManager</c> 直读值，换算与可信判据在 <see cref="BattleEntityMetrics"/>。
    /// </summary>
    public BattleEntityMetrics? BattleEntityMetrics {
        get {
            var em = _entityManager;
            if (em == null)
                return null;
            return new BattleEntityMetrics(
                em.Tickrate, (int)em.ServerSendRate,
                em.Tick, em.LastProcessedTick, em.LastReceivedTick,
                em.ServerTick, em.RawServerTick, em.RawTargetServerTick,
                em.StoredCommands, em.EntitiesCount, em.ServerInputBuffer,
                em.LerpBufferCount, em.LerpBufferTimeLength,
                em.NetworkJitter, em.AverageJitter, em.StateSize, em.PendingToRemoveEntites,
                _stateSpreadAvgPerSecond, _stateSpreadMaxPerSecond);
        }
    }

    /// <summary>
    /// 每帧采样正在播的 A 与目标 B 之间的服务端 tick 差。该差是 LES 插值节拍的乘数：
    /// 恒为 1 说明状态连续，跳到 2 以上即下行状态缺号，消费速率跌到 tickrate/倍。
    /// </summary>
    private void SampleStateSpread() {
        if (_entityManager is not { } em)
            return;
        int spread = Utils.SequenceDiff(em.RawTargetServerTick, em.RawServerTick);
        if (spread < 0)
            return;
        _spreadSamples++;
        _spreadSum += spread;
        if (spread > _spreadMax)
            _spreadMax = spread;
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

    /// <summary>清零传输与状态流统计，每秒结算由 UpdateAfterPollEvents 处理，此处仅全量清零。</summary>
    private void ResetTrafficCounters() {
        _bytesIn = 0;
        _packetsIn = 0;
        _secondAccumulator = 0;
        _packetsInPerSecond = 0;
        _bytesInPerSecond = 0;
        _packetsOutPerSecond = 0;
        _bytesOutPerSecond = 0;
        _spreadSamples = 0;
        _spreadSum = 0;
        _spreadMax = 0;
        _stateSpreadAvgPerSecond = 0;
        _stateSpreadMaxPerSecond = 0;
        _countingPeer = null;
    }

    #endregion
}
