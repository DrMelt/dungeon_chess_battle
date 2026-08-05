using System.Collections.Concurrent;
using System.Diagnostics;
using LiteNetLib;
using LiteEntitySystem;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Settings;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 单房间的 LES 实体服务器。每个房间拥有独立的 NetManager + ServerEntityManager，
/// 独立的 Logic 实例 (GameLogicService + RoomManager)，并运行在独立线程中，
/// 实现物理级别的 Entity 同步隔离与房间数据所有权。
/// 战斗流程面向 IBattle 抽象接口。
/// 创建 Entity 时仅该房间内的客户端可见。
/// 支持断线重连：通过 playerId 白名单验证连接请求，保留断连玩家的 Entity。
/// 网络事件见 BattleRoomServer.NetworkEvents，玩家会话见 BattleRoomServer.PlayerSession，
/// 单位与战斗见 BattleRoomServer.Battle。
/// </summary>
public partial class BattleRoomServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ILogger<BattleRoomServer> _logger;
    private readonly string _connectionKey;
    private const byte PacketHeader = 0xDC;
    private const double TickInterval = 0.02; // 50 Hz
    private const double ReconnectGracePeriodSeconds = 30.0;

    // 房间线程
    private Thread? _loopThread;
    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    /// <summary>playerId → PlayerSession 聚合映射（线程安全）</summary>
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();
    /// <summary>peer.Id → playerId 反向索引（断开时快速查找）</summary>
    private readonly ConcurrentDictionary<int, string> _peerToPlayerId = new();

    /// <summary>合法 playerId 白名单（活跃 + 宽限期内）。也可用 _sessions.Keys 替代，保留独立集合以加速 OnConnectionRequest 热路径。</summary>
    private readonly ConcurrentDictionary<string, byte> _validPlayerIds = new();
    /// <summary>已接受的连接密钥队列（OnConnectionRequest 入队，OnPeerConnected 出队）。
    /// P3-8 分析：NetPeer 不暴露 EndPoint 属性，无法使用按地址匹配的字典方案。
    /// 房间在单线程中顺序调用 PollEvents()，OnConnectionRequest 与 OnPeerConnected 在
    /// 同一轮询周期内以 FIFO 顺序处理，不存在跨连接错位的竞态条件。
    /// 保留 ConcurrentQueue 以保证线程安全。</summary>
    private readonly ConcurrentQueue<string> _acceptedKeys = new();

    /// <summary>本房间的所有 UnitPawn</summary>
    private readonly List<UnitPawn> _roomPawns = [];

    /// <summary>本房间的 BattleRoomEntity（SEM 创建后填充）</summary>
    private BattleRoomEntity? _roomEntity;

    /// <summary>本房间独立的 Logic 实例（不再共享全局）</summary>
    private readonly GameLogicService _logicService = new();

    /// <summary>本房间的战斗流程（面向 IBattle 抽象接口）</summary>
    private IBattle? _battle;

    /// <summary>实体管理器。</summary>
    public ServerEntityManager EntityManager {
        get;
    }

    /// <summary>监听端口。</summary>
    public int Port {
        get;
    }

    /// <summary>房间标识。</summary>
    public string RoomId {
        get;
    }

    /// <summary>当前连接数。</summary>
    public int PeerCount => _netManager.ConnectedPeersCount;

    /// <summary>房间是否已无任何玩家会话（活跃 + 宽限期内均无）。</summary>
    public bool IsRoomEmpty => _sessions.IsEmpty;

    /// <summary>仅用于调试/测试，不应在运行时由外部线程访问</summary>
    internal UnitPawn[] GetPawnsSnapshot() => [.. _roomPawns];

    /// <summary>房间服务器是否正在运行</summary>
    public bool IsRunning => _running;

    /// <summary>客户端连接事件。参数：peer ID。</summary>
    public event Action<int>? OnClientConnected;

    /// <summary>客户端断开事件。参数：peer ID。</summary>
    public event Action<int>? OnClientDisconnected;

    /// <summary>玩家彻底离开房间事件（超出宽限期后触发）</summary>
    public event Action<string, string>? PlayerRemoved; // (roomId, playerId)

    /// <param name="port">监听端口</param>
    /// <param name="roomId">房间标识</param>
    /// <param name="logger">日志器</param>
    /// <param name="config">服务器配置（连接密钥）。</param>
    public BattleRoomServer(int port, string roomId, ILogger<BattleRoomServer> logger, ServerConfig config) {
        Port = port;
        RoomId = roomId;
        _logger = logger;
        _connectionKey = config.ServerPassword ?? config.ConnectionKey;

        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        EntityManager = new ServerEntityManager(
            typesMap,
            PacketHeader,
            framesPerSecond: 60,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    /// <summary>
    /// 启动房间服务器：创建 BattleRoomEntity 与 Logic 房间，并启动独立线程主循环。
    /// </summary>
    public void Start() {
        _netManager.Start(Port);

        // 在房间 SEM 中创建 BattleRoomEntity，并订阅其实例事件
        _roomEntity = EntityManager.AddEntity<BattleRoomEntity>(e => {
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

    /// <summary>
    /// 停止房间服务器：取消订阅事件、清理重连数据并关闭网络。
    /// </summary>
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

        // 闭合 Logic 层生命周期：清理 RoomManager 中本房间的 GameRoom 与 BattleManager
        _logicService.RemoveRoom(RoomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer] Room '{RoomId}' stopped on port {Port}", RoomId, Port);
    }

    /// <summary>
    /// 房间服务器主循环（独立线程）：轮询网络事件并以固定间隔驱动实体同步与战斗逻辑。
    /// </summary>
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
                    EntityManager.Update();

                    // 3. 战斗 Tick + Buff 更新
                    if (_battle?.CurrentPhase == BattlePhase.Running) {
                        _battle.Tick((float)dt);

                        var gameRoom = _logicService.GetRoom(RoomId);
                        if (gameRoom != null)
                            _logicService.UpdateBuffs(gameRoom.UnitsA.Concat(gameRoom.UnitsB), dt);
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
}
