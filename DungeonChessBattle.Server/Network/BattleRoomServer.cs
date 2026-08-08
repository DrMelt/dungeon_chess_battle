using System.Collections.Concurrent;
using System.Diagnostics;
using LiteNetLib;
using LiteEntitySystem;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Settings;
using DungeonChessBattle.Server.Stores;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Network;

/// <summary>
/// 单房间的 LES 实体服务器。每个房间拥有独立的 NetManager + ServerEntityManager，
/// 独立的 Logic 实例 (GameLogicService + RoomManager)，并运行在独立线程中，
/// 实现物理级别的 Entity 同步隔离与房间数据所有权。
/// 战斗流程面向 IBattle 抽象接口。
/// 创建 Entity 时仅该房间内的客户端可见。
/// 支持断线重连：连接资格实时查询 <see cref="IGameStateStore"/>（房间存在期间
/// 登记成员可连接）；断线玩家实体保留直至房间销毁（无宽限期机制）。
/// 网络事件见 BattleRoomServer.NetworkEvents，玩家会话见 BattleRoomServer.PlayerSession，
/// 单位与战斗见 BattleRoomServer.Battle。
/// 线程所有权：EntityManager 的所有操作（初始化、CreatePawnEntity、RPC、Update）
/// 全部发生在房间线程；大厅线程只负责生命周期管理（启动、等待初始化、停止）。
/// </summary>
public partial class BattleRoomServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ILogger<BattleRoomServer> _logger;
    private readonly string _connectionKey;
    private readonly IGameStateStore _stateStore;
    private const byte PacketHeader = 0xDC;
    private const double TickInterval = 0.02; // 50 Hz

    // 房间线程
    private Thread? _loopThread;
    private volatile bool _running;
    private readonly Stopwatch _tickWatch = Stopwatch.StartNew();
    private double _lastTickTime;

    /// <summary>初始化完成信号：房间线程首帧完成根实体创建与单位迁移后置位。</summary>
    private readonly ManualResetEventSlim _initialized = new(false);

    /// <summary>playerId → PlayerSession 聚合映射（线程安全）</summary>
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();
    /// <summary>peer.Id → playerId 反向索引（断开时快速查找）</summary>
    private readonly ConcurrentDictionary<int, string> _peerToPlayerId = new();

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

    /// <summary>是否有任意活跃的客户端连接（断线保留实体的玩家不计入）。</summary>
    public bool HasActiveConnections => !_peerToPlayerId.IsEmpty;

    /// <summary>仅用于调试/测试，不应在运行时由外部线程访问</summary>
    internal UnitPawn[] GetPawnsSnapshot() => [.. _roomPawns];

    /// <summary>房间服务器是否正在运行</summary>
    public bool IsRunning => _running;

    /// <summary>客户端连接事件。参数：peer ID。</summary>
    public event Action<int>? OnClientConnected;

    /// <summary>客户端断开事件。参数：peer ID。</summary>
    public event Action<int>? OnClientDisconnected;

    /// <summary>房间无任何活跃连接事件（房间线程触发；消费方负责在线程边界外执行销毁）。</summary>
    public event Action<string>? RoomEmpty;

    /// <param name="port">监听端口</param>
    /// <param name="roomId">房间标识</param>
    /// <param name="logger">日志器</param>
    /// <param name="config">服务器配置（连接密钥）。</param>
    /// <param name="stateStore">大厅级状态存储（房间线程用于自取初始化数据与成员校验）。</param>
    public BattleRoomServer(int port, string roomId, ILogger<BattleRoomServer> logger,
        ServerConfig config, IGameStateStore stateStore) {
        Port = port;
        RoomId = roomId;
        _logger = logger;
        _connectionKey = config.ServerPassword ?? config.ConnectionKey;
        _stateStore = stateStore;

        var typesMap = EntityTypesRegistry.GetOrCreateMap();
        EntityManager = new ServerEntityManager(
            typesMap,
            PacketHeader,
            framesPerSecond: 60,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    /// <summary>
    /// 启动房间服务器：启动网络与独立线程主循环。
    /// 根实体创建、Logic 房间创建与准备期单位迁移均在线程 B（房间线程）首帧执行，
    /// 保证 EntityManager 的所有操作收敛到单一线程。
    /// </summary>
    public void Start() {
        _netManager.Start(Port);

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
    /// 等待房间线程完成首帧初始化（根实体、Logic 房间与单位迁移）。
    /// 配合 StartRoomBattle：初始化完成后才广播重定向，保证客户端连入时
    /// 房间已就绪。返回 false 表示等待超时。
    /// </summary>
    public bool WaitUntilInitialized(TimeSpan timeout) => _initialized.Wait(timeout);

    /// <summary>
    /// 停止房间服务器：取消订阅事件、清理全部会话与重连数据并关闭网络。
    /// 应由大厅线程调用（本方法会 Join 房间线程）。
    /// </summary>
    public void Stop() {
        _running = false;

        // 取消订阅 Entity 事件
        if (_roomEntity != null) {
            _roomEntity.CreateUnitRequested -= OnCreateUnitRequested;
            _roomEntity.StartBattleRequested -= OnStartBattleRequested;
        }

        // 取消订阅所有 Pawn 的 SkillCast 与输入事件
        foreach (var pawn in _roomPawns) {
            pawn.SkillCastRequested -= OnPawnSkillCast;
            pawn.InputReceived -= OnPawnInput;
        }

        // 先等待房间线程退出，再销毁保留实体（避免大厅线程在房间线程
        // 仍运行 EntityManager.Update() 时并发销毁实体）
        _loopThread?.Join(TimeSpan.FromSeconds(3));
        _netManager.Stop();

        // 销毁全部保留实体（断线玩家实体随房间销毁一并清理；房间线程已退出）
        CleanupAllSessions();

        // 闭合 Logic 层生命周期：清理 RoomManager 中本房间的 GameRoom 与 BattleManager
        _logicService.RemoveRoom(RoomId);

        // 释放初始化信号（不再有等待方）
        _initialized.Dispose();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[RoomServer] Room '{RoomId}' stopped on port {Port}", RoomId, Port);
    }

    /// <summary>
    /// 房间服务器主循环（独立线程）：首帧初始化后轮询网络事件并以固定间隔
    /// 驱动实体同步与战斗逻辑。
    /// </summary>
    private void RunLoop() {
        // 首帧初始化：根实体、Logic 房间与准备期单位迁移全部在房间线程完成
        try {
            InitializeFromStore();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[RoomServer:{RoomId}] Initialization failed.", RoomId);
        }
        finally {
            // 即使初始化失败也放行，避免大厅线程 WaitUntilInitialized 无限等待
            _initialized.Set();
        }

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

                    // 4. Pawn 冷却更新 + Logic 模型回写到 Pawn（Health / Position）
                    SyncLogicModelToPawns((float)dt);

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

    /// <summary>
    /// 每帧递减 Pawn 冷却，并将 Logic 层模型的最新状态回写到网络 Pawn（健康值、位置）。
    /// 仅房间线程调用。
    /// </summary>
    /// <param name="dt">距上一帧的间隔时间（秒）。</param>
    private void SyncLogicModelToPawns(float dt) {
        var gameRoom = _logicService.GetRoom(RoomId);
        if (gameRoom == null)
            return;

        foreach (var pawn in _roomPawns) {
            pawn.UpdateCooldowns(dt);

            var model = gameRoom.UnitsA.Concat(gameRoom.UnitsB)
                .FirstOrDefault(u => u.UnitStateName == pawn.UnitName.Value);
            if (model == null)
                continue;

            if (MathF.Abs(pawn.Health.Value - model.Health) > 0.0001f)
                pawn.ServerSetHealth(model.Health);

            var modelPos = model.Position;
            var pawnPos = new System.Numerics.Vector2(modelPos.X, modelPos.Z);
            if (System.Numerics.Vector2.DistanceSquared(pawn.Position.Value, pawnPos) > 0.0001f) {
                _logger.LogInformation("[RoomServer:{RoomId}] SyncPos: {Unit} = ({X}, {Z})",
                    RoomId, pawn.UnitName.Value, modelPos.X, modelPos.Z);
                pawn.Position.Value = pawnPos;
            }
        }
    }

    /// <summary>仅房间线程内部调用，禁止外部并发调用（LiteNetLib.PollEvents 非线程安全）</summary>
    internal void PollEvents() {
        _netManager.PollEvents();
    }
}
