using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Entities.SyncData;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 单房间的 LES 实体服务器。每个房间拥有独立的 NetManager + ServerEntityManager，
/// 独立的 Logic 实例 (GameLogicService + RoomManager + BattleManager)，
/// 并运行在独立线程中，实现物理级别的 Entity 同步隔离与房间数据所有权。
/// 创建 Entity 时仅该房间内的客户端可见。
/// 支持断线重连：通过 playerId 白名单验证连接请求，保留断连玩家的 Entity。
/// </summary>
public class RoomEntityServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ServerEntityManager _entityManager;
    private readonly ILogger<RoomEntityServer> _logger;
    private const string DefaultConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;
    private const double TickInterval = 0.02; // 50 Hz
    private const double ReconnectGracePeriodSeconds = 30.0;

    // 房间线程
    private Thread? _loopThread;
    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    // ── 玩家会话（P2-5：7 个独立字典合并为 2 个） ──────
    /// <summary>playerId → PlayerSession 聚合映射（线程安全）</summary>
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();
    /// <summary>peer.Id → playerId 反向索引（断开时快速查找）</summary>
    private readonly ConcurrentDictionary<int, string> _peerToPlayerId = new();

    // ── 连接验证 ─────────────────────────────────────────
    /// <summary>合法 playerId 白名单（活跃 + 宽限期内）。也可用 _sessions.Keys 替代，保留独立集合以加速 OnConnectionRequest 热路径。</summary>
    private readonly ConcurrentDictionary<string, byte> _validPlayerIds = new();
    /// <summary>已接受的连接密钥队列（OnConnectionRequest 入队，OnPeerConnected 出队）。
    /// P3-8 分析：NetPeer 不暴露 EndPoint 属性，无法使用按地址匹配的字典方案。
    /// 房间在单线程中顺序调用 PollEvents()，OnConnectionRequest 与 OnPeerConnected 在
    /// 同一轮询周期内以 FIFO 顺序处理，不存在跨连接错位的竞态条件。
    /// 保留 ConcurrentQueue 以保证线程安全。</summary>
    private readonly ConcurrentQueue<string> _acceptedKeys = new();

    // ── 房间数据所有权（从 GameLobby 迁移） ──────────────
    /// <summary>本房间的所有 UnitPawn</summary>
    private readonly List<UnitPawn> _roomPawns = [];

    /// <summary>本房间的 BattleRoomEntity（SEM 创建后填充）</summary>
    private BattleRoomEntity? _roomEntity;

    /// <summary>本房间独立的 Logic 实例（不再共享全局）</summary>
    private readonly GameLogicService _logicService = new();

    /// <summary>本房间的战斗管理器</summary>
    private BattleManager? _battle;

    // ── 公开属性 ─────────────────────────────────────────
    public ServerEntityManager EntityManager => _entityManager;
    public int Port {
        get;
    }
    public string RoomId {
        get;
    }
    public int PeerCount => _netManager.ConnectedPeersCount;
    /// <summary>仅用于调试/测试，不应在运行时由外部线程访问</summary>
    internal UnitPawn[] GetPawnsSnapshot() => [.. _roomPawns];

    /// <summary>房间服务器是否正在运行</summary>
    public bool IsRunning => _running;

    public event Action<int>? OnClientConnected;
    public event Action<int>? OnClientDisconnected;

    /// <summary>玩家彻底离开房间事件（超出宽限期后触发）</summary>
    public event Action<string, string>? PlayerRemoved; // (roomId, playerId)

    /// <param name="port">监听端口</param>
    /// <param name="roomId">房间标识</param>
    /// <param name="logger">日志器</param>
    public RoomEntityServer(int port, string roomId, ILogger<RoomEntityServer> logger) {
        Port = port;
        RoomId = roomId;
        _logger = logger;

        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        _entityManager = new ServerEntityManager(
            typesMap,
            PacketHeader,
            framesPerSecond: 60,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    // ── 生命周期 ─────────────────────────────────────────

    public void Start() {
        _netManager.Start(Port);

        // 在房间 SEM 中创建 BattleRoomEntity，并订阅其实例事件
        _roomEntity = _entityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = RoomId;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{RoomId}'.");

        _roomEntity.CreateUnitRequested += OnCreateUnitRequested;
        _roomEntity.StartBattleRequested += OnStartBattleRequested;

        // 创建 Logic 层房间
        _logicService.CreateRoom(RoomId);

        _running = true;
        _lastTickTime = _tickWatch.Elapsed.TotalSeconds;
        _loopThread = new Thread(RunLoop) {
            Name = $"Room-{RoomId}",
            IsBackground = true
        };
        _loopThread.Start();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer] Room '{RoomId}' started on port {Port} (thread: {ThreadName})",
                RoomId, Port, _loopThread.Name);
    }

    public void Stop() {
        _running = false;

        // 取消订阅 Entity 事件
        if (_roomEntity != null) {
            _roomEntity.CreateUnitRequested -= OnCreateUnitRequested;
            _roomEntity.StartBattleRequested -= OnStartBattleRequested;
        }

        // 取消订阅所有 Pawn 的 SkillCast 事件
        foreach (var pawn in _roomPawns)
            pawn.SkillCastRequested -= OnPawnSkillCast;

        // 清理所有重连管理数据
        _validPlayerIds.Clear();
        _sessions.Clear();
        // _acceptedKeys 是 ConcurrentQueue，无需 Clear

        _loopThread?.Join(TimeSpan.FromSeconds(3));
        _netManager.Stop();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer] Room '{RoomId}' stopped on port {Port}", RoomId, Port);
    }

    // ── 主循环（独立线程） ───────────────────────────────

    private void RunLoop() {
        while (_running) {
            try {
                double now = _tickWatch.Elapsed.TotalSeconds;
                double dt = now - _lastTickTime;

                // 1. 网络事件
                _netManager.PollEvents();

                if (dt >= TickInterval) {
                    _lastTickTime = now;

                    // 2. Entity 同步
                    _entityManager.Update();

                    // 3. 战斗 Tick + Buff 更新
                    if (_battle?.CurrentPhase == BattlePhase.Running) {
                        _battle.Tick((float)dt);

                        var gameRoom = _logicService.GetRoom(RoomId);
                        if (gameRoom != null)
                            GameLogicService.UpdateBuffs(_battle, gameRoom.UnitsA.Concat(gameRoom.UnitsB), dt);
                    }

                    // 4. Pawn 冷却更新 + Service→Entity Health 同步
                    foreach (var pawn in _roomPawns) {
                        pawn.UpdateCooldowns((float)dt);

                        var gameRoom = _logicService.GetRoom(RoomId);
                        if (gameRoom != null) {
                            var model = gameRoom.UnitsA.Concat(gameRoom.UnitsB)
                                .FirstOrDefault(u => u.UnitStateName == pawn.UnitName.Value);
                            if (model != null && MathF.Abs(pawn.Health.Value - model.Health) > 0.0001f)
                                pawn.ServerSetHealth(model.Health);
                        }
                    }

                    // 5. 战斗结束检查
                    CheckBattleEnded();

                    // 6. 断连宽限期超时清理
                    CleanupExpiredPlayers();
                }

                Thread.Sleep(1);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "[RoomServer:{RoomId}] Unhandled exception in RunLoop. Room continues.", RoomId);
            }
        }
    }

    /// <summary>仅房间线程内部调用，禁止外部并发调用（LiteNetLib.PollEvents 非线程安全）</summary>
    internal void PollEvents() {
        _netManager.PollEvents();
    }

    // ── 公开方法（供 GameServer 大厅层调用） ─────────────

    /// <summary>
    /// 大厅层预注册玩家到白名单。客户端真正连接房间端口前调用。
    /// </summary>
    public void RegisterPlayer(string playerId, string playerName) {
        _validPlayerIds[playerId] = 1;
        _sessions.GetOrAdd(playerId, _ => new PlayerSession(playerId, playerName));
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) pre-registered.", RoomId, playerName, playerId);
    }

    /// <summary>
    /// 大厅层查询玩家是否可重连（需在宽限期内）。
    /// </summary>
    public bool CanReconnect(string playerId) {
        return _sessions.TryGetValue(playerId, out var session) && session.DisconnectTime != null;
    }

    /// <summary>
    /// 更新已注册玩家的显示名（重连时可能更改）。
    /// </summary>
    public void UpdatePlayerName(string playerId, string playerName) {
        if (_sessions.TryGetValue(playerId, out var session)) {
            session.PlayerName = playerName;
            if (session.Entity != null)
                session.Entity.PlayerName.Value = playerName;
        }
        else {
            // 预注册阶段（尚未创建 Entity + Session），创建 session
            _sessions[playerId] = new PlayerSession(playerId, playerName);
        }
    }

    // ── INetEventListener ─────────────────────────────────

    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        string incomingKey = request.Data.GetString();

        // 验证：playerId 在白名单中 或 使用默认连接密钥（向后兼容/调试模式）
        if (incomingKey == DefaultConnectionKey || _validPlayerIds.ContainsKey(incomingKey)) {
            _acceptedKeys.Enqueue(incomingKey);
            request.Accept();
        }
        else {
            if (_logger.IsEnabled(LogLevel.Warning))
                _logger.LogWarning("[RoomServer:{RoomId}] Connection rejected: invalid key from {RemoteEP}", RoomId, request.RemoteEndPoint);
            request.Reject();
        }
    }

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        // 提取连接时使用的密钥（即 playerId 或默认密钥）
        _acceptedKeys.TryDequeue(out string? connectionKey);

        // P1 修复：同一 playerId 已有活跃连接时，关闭旧连接接受新连接
        if (connectionKey != null && connectionKey != DefaultConnectionKey
            && _sessions.TryGetValue(connectionKey, out var existingSession)
            && existingSession.Entity != null) {
            if (existingSession.Entity.PlayerState.Value == (byte)PlayerConnectionState.Connected) {
                // 替换：清理旧 peer，用新 peer 重连
                _logger.LogInformation("[RoomServer:{RoomId}] Duplicate connection for playerId '{PlayerId}', replacing old peer.",
                    RoomId, connectionKey);
                ReplaceExistingConnection(connectionKey);
            }
            // 执行重连流程（Disconnected → 恢复 或 替换后重新绑定）
            HandlePlayerReconnect(peer, connectionKey);
        }
        else {
            HandleNewPlayerConnect(peer, connectionKey);
        }

        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (peer.Tag is LiteNetLibNetPeer lesPeer)
            _entityManager.RemovePlayer(lesPeer);

        // 查找该 peer 对应的 playerId（通过反向索引）
        _peerToPlayerId.TryRemove(peer.Id, out string? playerId);
        if (playerId != null && _sessions.TryGetValue(playerId, out var session)) {
            // 标记为断连状态（保留 Entity，不销毁）
            if (session.Entity != null)
                session.Entity.PlayerState.Value = (byte)PlayerConnectionState.Disconnected;
            session.DisconnectTime = DateTime.UtcNow;
            // 注意：不删除 _sessions 和 _validPlayerIds
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerId}' disconnected (reconnect grace period started).",
                    RoomId, playerId);
        }

        // 清除旧 peer 的引用
        if (playerId != null && _sessions.TryGetValue(playerId, out var s)) {
            s.NetPlayer = null;
            s.Controller = null;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Peer disconnected: {PeerId}, playerId={PlayerId}, Reason={Reason}",
                RoomId, peer.Id, playerId, disconnectInfo.Reason);

        OnClientDisconnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnNetworkError(IPEndPoint endPoint, SocketError socketError) {
        _logger.LogError("[RoomServer:{RoomId}] Error: {SocketError} from {EndPoint}", RoomId, socketError, endPoint);
    }

    void INetEventListener.OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channelNumber, DeliveryMethod deliveryMethod) {
        var data = reader.GetRemainingBytes();
        if (data.Length > 0 && data[0] == PacketHeader) {
            if (peer.Tag is LiteNetLibNetPeer lesPeer)
                _entityManager.Deserialize(lesPeer, data);
        }
        // 房间端口不处理 JSON 自定义包（所有逻辑走 LES RPC）
    }

    void INetEventListener.OnNetworkReceiveUnconnected(IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType) {
    }
    void INetEventListener.OnNetworkLatencyUpdate(NetPeer peer, int latency) {
    }

    // ── 玩家连接处理 ─────────────────────────────────────

    /// <summary>
    /// 处理新玩家首次连接：创建 PlayerSession + PlayerRoomEntity。
    /// </summary>
    private void HandleNewPlayerConnect(NetPeer peer, string? connectionKey) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var netPlayer = _entityManager.AddPlayer(lesPeer);

        // 确定 playerId（优先使用连接密钥中的 playerId）
        string effectivePlayerId = (connectionKey != null && connectionKey != DefaultConnectionKey)
            ? connectionKey
            : $"auto_{peer.Id}";

        // 获取或新建 PlayerSession
        var session = _sessions.GetOrAdd(effectivePlayerId,
            _ => new PlayerSession(effectivePlayerId, $"Player_{effectivePlayerId[..Math.Min(effectivePlayerId.Length, 8)]}"));

        // 确定玩家显示名
        string playerName = session.PlayerName;
        string displayId = session.DisplayId;

        // 创建 PlayerRoomEntity
        var playerEntity = _entityManager.AddEntity<PlayerRoomEntity>(e => {
            e.PlayerName.Value = playerName;
            e.DisplayId.Value = displayId;
            e.PlayerState.Value = (byte)PlayerConnectionState.Connected;
            e.IsReady.Value = false;
            e.Camp.Value = 0;
        });

        if (playerEntity != null) {
            session.PeerId = peer.Id;
            session.Entity = playerEntity;
            session.NetPlayer = netPlayer;
            session.DisconnectTime = null;
            _peerToPlayerId[peer.Id] = effectivePlayerId;

            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] PlayerRoomEntity created: '{PlayerName}' (display={DisplayId}), peer={PeerId}",
                    RoomId, playerName, displayId, peer.Id);
        }
    }

    /// <summary>
    /// 处理玩家重连：将新网络连接绑定到已有的 PlayerSession。
    /// </summary>
    private void HandlePlayerReconnect(NetPeer peer, string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session) || session.Entity == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Reconnect: entity not found for playerId '{PlayerId}', treating as new.", RoomId, playerId);
            HandleNewPlayerConnect(peer, playerId);
            return;
        }

        // 清除宽限期计时器
        session.DisconnectTime = null;

        // 恢复连接状态
        session.Entity.PlayerState.Value = (byte)PlayerConnectionState.Connected;

        // 重建网络层绑定
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var netPlayer = _entityManager.AddPlayer(lesPeer);
        session.PeerId = peer.Id;
        session.NetPlayer = netPlayer;
        _peerToPlayerId[peer.Id] = playerId;

        // 客户端通过 PlayerState SyncVar 从 Disconnected→Connected 的变化检测重连成功

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) reconnected (peer={PeerId}).",
                RoomId, session.Entity.PlayerName.Value, playerId, peer.Id);
    }

    /// <summary>
    /// 清理旧连接的所有映射（用于新连接替换旧连接场景）。
    /// 不触发 OnPeerDisconnected 的宽限期逻辑，不修改 PlayerState。
    /// </summary>
    private void ReplaceExistingConnection(string playerId) {
        if (!_sessions.TryGetValue(playerId, out var session))
            return;

        int oldPeerId = session.PeerId;

        // 从 LES 框架移除旧玩家
        if (session.NetPlayer != null)
            _entityManager.RemovePlayer(session.NetPlayer);

        _peerToPlayerId.TryRemove(oldPeerId, out _);
        session.NetPlayer = null;
        session.Controller = null;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[RoomServer:{RoomId}] Old peer {OldPeerId} disconnected for playerId '{PlayerId}' (replaced by new).",
                RoomId, oldPeerId, playerId);
    }

    // ── 断连清理 ──────────────────────────────────────────

    /// <summary>
    /// 检查并清理超出宽限期的断连玩家。
    /// </summary>
    private void CleanupExpiredPlayers() {
        var now = DateTime.UtcNow;
        var expiredIds = _sessions
            .Where(kv => kv.Value.DisconnectTime.HasValue
                && (now - kv.Value.DisconnectTime.Value).TotalSeconds > ReconnectGracePeriodSeconds)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var playerId in expiredIds) {
            CleanupPlayer(playerId);
        }
    }

    /// <summary>
    /// 彻底清理指定玩家：从白名单移除，销毁 PlayerRoomEntity 和关联的 UnitPawn。
    /// </summary>
    private void CleanupPlayer(string playerId) {
        _validPlayerIds.TryRemove(playerId, out _);

        if (_sessions.TryRemove(playerId, out var session)) {
            // 销毁 LES Entity（InternalEntity.Destroy），同步移除到所有客户端
            session.Entity?.Destroy();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] Player '{PlayerName}' ({PlayerId}) cleanup completed (timeout + entity destroyed).",
                    RoomId, session.PlayerName, playerId);
        }

        // 通知外部（GameLobby 可据此判断是否需要销毁空房间）
        PlayerRemoved?.Invoke(RoomId, playerId);
    }

    // ── Pawn 管理 ────────────────────────────────────────

    /// <summary>
    /// 在本房间的 SEM 中创建 UnitPawn 实体。
    /// </summary>
    public UnitPawn CreatePawnEntity(string unitName, byte camp, Vector2 spawnPos) {
        var entity = _entityManager.AddEntity<UnitPawn>(e => {
            e.UnitName.Value = unitName;
            e.Camp.Value = camp;
            e.Position.Value = spawnPos;
        }) ?? throw new InvalidOperationException($"Failed to create UnitPawn for unit '{unitName}' in room '{RoomId}'.");

        // 订阅该 Pawn 的技能 RPC 事件
        entity.SkillCastRequested += OnPawnSkillCast;

        _roomPawns.Add(entity);

        // Logic 层创建单位
        _logicService.CreateUnit(RoomId, unitName, camp);

        return entity;
    }

    /// <summary>
    /// 在本房间范围内按 NetId 查找 UnitPawn，不再跨房间查找。
    /// </summary>
    public UnitPawn? FindPawnById(ushort netId) {
        return _roomPawns.Find(p => p.Id == netId);
    }

    // ── 战斗管理 ──────────────────────────────────────────

    /// <summary>
    /// 在本房间启动战斗。
    /// </summary>
    public BattleManager StartBattle() {
        if (_battle != null && _battle.CurrentPhase == BattlePhase.Running)
            return _battle;

        _battle = _logicService.StartBattleInRoom(RoomId);
        _battle.BattleStarted += OnBattleStarted;
        _battle.BattleEnded += OnBattleEnded;
        return _battle;
    }

    /// <summary>
    /// 获取本房间的 BattleRoomEntity。
    /// </summary>
    public BattleRoomEntity? GetRoomEntity() => _roomEntity;

    // ── RPC 事件处理（实例事件，仅本房间） ─────────────────

    private void OnCreateUnitRequested(BattleRoomEntity roomEntity, SyncCreateUnitRequest req) {
        var spawnPos = req.Camp == 1
            ? new Vector2(0, 0)
            : new Vector2(5, 0);

        CreatePawnEntity(req.UnitName, req.Camp, spawnPos);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Unit created via RPC: {UnitName}, camp={Camp}",
                RoomId, req.UnitName, req.Camp);
    }

    private void OnStartBattleRequested(BattleRoomEntity roomEntity) {
        StartBattle();
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Battle started via RPC", RoomId);
    }

    /// <summary>
    /// 处理通过 UnitPawn 实例事件到达的技能施放请求。
    /// </summary>
    private void OnPawnSkillCast(UnitPawn casterPawn, SyncSkillRequest req) {
        var targetPawn = FindPawnById(req.TargetUnitNetId);
        if (targetPawn == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: target pawn {TargetId} not found.", RoomId, req.TargetUnitNetId);
            return;
        }

        var casterModel = _logicService.FindUnitModel(casterPawn.UnitName.Value);
        var targetModel = _logicService.FindUnitModel(targetPawn.UnitName.Value);
        if (casterModel == null || targetModel == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: unit model not found in Logic layer.", RoomId);
            return;
        }

        if (_battle == null) {
            _logger.LogWarning("[RoomServer:{RoomId}] Skill RPC: no active battle.", RoomId);
            return;
        }

        float oldTargetHealth = targetModel.Health;

        if (req.IsDamage) {
            var skill = new SkillDamageModel {
                Damage = req.DamageOrCureValue,
                DamageType = (Enum_DamageType)req.DamageType
            };
            GameLogicService.CastSkill(_battle, casterModel, targetModel, skill);
        }
        else {
            var skill = new SkillCureModel { CurePotency = -req.DamageOrCureValue };
            GameLogicService.CastSkill(_battle, casterModel, targetModel, skill);
        }

        targetPawn.ServerSetHealth(targetModel.Health);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Skill result: {Caster} -> {Target}, HP: {OldHealth:F0} -> {NewHealth:F0}",
                RoomId, casterPawn.UnitName.Value, targetPawn.UnitName.Value, oldTargetHealth, targetPawn.Health.Value);
    }

    // ── 内部辅助 ──────────────────────────────────────────

    private void OnBattleStarted() {
        if (_roomEntity != null) {
            _roomEntity.BattlePhase.Value = (byte)BattlePhase.Running;
            _roomEntity.IsFinished.Value = false;
        }
    }

    private void OnBattleEnded() {
        if (_roomEntity != null) {
            _roomEntity.BattlePhase.Value = (byte)BattlePhase.Finished;
            _roomEntity.IsFinished.Value = true;

            var gameRoom = _logicService.GetRoom(RoomId);
            if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
                _roomEntity.WinnerCamp.Value = (byte)(
                    BattleResolver.HasAliveUnits(gameRoom.UnitsA) ? 1u : 2u);
            }
        }
    }

    private void CheckBattleEnded() {
        if (_battle?.CurrentPhase != BattlePhase.Running)
            return;

        var gameRoom = _logicService.GetRoom(RoomId);
        if (gameRoom != null && _logicService.CheckBattleEnded(gameRoom)) {
            GameLogicService.EndBattle(_battle);
        }
    }
}