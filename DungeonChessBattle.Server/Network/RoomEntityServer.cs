using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using LiteNetLib;
using LiteEntitySystem;
using LiteEntitySystem.Transport;
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
/// </summary>
public class RoomEntityServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ServerEntityManager _entityManager;
    private readonly ILogger<RoomEntityServer> _logger;
    private const string ConnectionKey = "DungeonChessBattle";
    private const byte PacketHeader = 0xDC;
    private const double TickInterval = 0.016; // 60 Hz

    // 房间线程
    private Thread? _loopThread;
    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    // 玩家实体跟踪（按 NetPeer.Id 索引）
    private readonly Dictionary<int, PlayerRoomEntity> _playerEntities = [];
    private readonly Dictionary<int, NetPlayer> _netPlayers = [];
    private readonly Dictionary<int, UnitController> _unitControllers = [];

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

    // ── INetEventListener ─────────────────────────────────

    void INetEventListener.OnConnectionRequest(ConnectionRequest request) {
        request.AcceptIfKey(ConnectionKey);
    }

    void INetEventListener.OnPeerConnected(NetPeer peer) {
        var lesPeer = new LiteNetLibNetPeer(peer, assignToTag: true);
        var player = _entityManager.AddPlayer(lesPeer);
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Peer connected: {PeerId}, PlayerId: {PlayerId}", RoomId, peer.Id, player?.Id);

        // 追踪 NetPlayer（用于创建 UnitController 等需要玩家引用的操作）
        if (player != null)
            _netPlayers[peer.Id] = player;

        // 为房间内每个客户端创建 PlayerRoomEntity
        var playerEntity = _entityManager.AddEntity<PlayerRoomEntity>(e => {
            e.PlayerName.Value = $"Player_{peer.Id}";
            e.IsReady.Value = false;
            e.Camp.Value = 0;
        });
        if (playerEntity != null) {
            _playerEntities[peer.Id] = playerEntity;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[RoomServer:{RoomId}] PlayerRoomEntity created for peer {PeerId}", RoomId, peer.Id);
        }

        OnClientConnected?.Invoke(peer.Id);
    }

    void INetEventListener.OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo) {
        if (peer.Tag is LiteNetLibNetPeer lesPeer)
            _entityManager.RemovePlayer(lesPeer);

        _playerEntities.Remove(peer.Id);
        _netPlayers.Remove(peer.Id);
        _unitControllers.Remove(peer.Id);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer:{RoomId}] Peer disconnected: {PeerId}, Reason: {Reason}", RoomId, peer.Id, disconnectInfo.Reason);
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
                DamageType = (Core.Enums.Enum_DamageType)req.DamageType
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
