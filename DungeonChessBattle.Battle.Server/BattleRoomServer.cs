using System.Collections.Concurrent;
using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Enums;
using DungeonChessBattle.Battle.Logic;
using DungeonChessBattle.Battle.Logic.Movement;
using DungeonChessBattle.Battle.Entities;
using DungeonChessBattle.GameConfig;
using DungeonChessBattle.Protocol;
using DungeonChessBattle.Server.DataStore.Shared;
using LiteEntitySystem;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Battle.Server;

/// <summary>
/// 单房间的 LES 实体服务器。每个房间拥有独立的 NetManager + ServerEntityManager，
/// 独立的战斗世界实例 BattleScene 与领域技能仓库 GameConfigDB，并运行在独立线程中，
/// 实现物理级别的 Entity 同步隔离与房间数据所有权。
/// 战斗流程由 BattleScene 统一驱动，读条、冷却、Buff、结算与阶段；
/// 房间级阶段状态由战斗世界投影写载体，战斗内领域事件经整帧事件日志广播到客户端。
/// 创建 Entity 时仅该房间内的客户端可见。
/// 支持断线重连：连接资格实时查询 <see cref="IGameStateStore"/>，房间存续期间
/// 登记成员可连接；断线玩家实体保留直至房间销毁，无宽限期机制。
/// 网络事件见 BattleRoomServer.NetworkEvents，玩家会话见 BattleRoomServer.PlayerSession，
/// 单位与战斗见 BattleRoomServer.Battle。
/// 线程所有权：EntityManager 的所有操作，初始化、CreatePawnEntity、RPC、Update，
/// 全部发生在房间线程；大厅线程只负责生命周期管理，启动、等待初始化、停止。
/// </summary>
public partial class BattleRoomServer : INetEventListener {
    private readonly NetManager _netManager;
    private readonly ILogger<BattleRoomServer> _logger;
    private readonly string _connectionKey;
    private readonly IGameStateStore _stateStore;

    private const int FramesPerSecond = 50;

    /// <summary>连续逻辑 tick 失败达到该次数后停止房间，避免错误状态无限运行。</summary>
    private const int MaxConsecutiveTickFailures = 10;

    // 房间线程
    private Thread? _loopThread;
    private volatile bool _running;

    /// <summary>首帧初始化是否成功，StartRoomBattle 等待初始化信号后据此判断。</summary>
    public bool InitializeSucceeded {
        get; private set;
    }

    /// <summary>初始化完成信号：房间线程首帧完成根实体创建与单位迁移后置位。</summary>
    private readonly ManualResetEventSlim _initialized = new(false);

    /// <summary>playerId 到 PlayerSession 的聚合映射，线程安全。</summary>
    private readonly ConcurrentDictionary<string, PlayerSession> _sessions = new();
    /// <summary>peer.Id 到 playerId 的反向索引，断开时快速查找。</summary>
    private readonly ConcurrentDictionary<int, string> _peerToPlayerId = new();

    /// <summary>已接受的连接密钥队列，OnConnectionRequest 入队，OnPeerConnected 出队。
    /// P3-8 分析：NetPeer 不暴露 EndPoint 属性，无法使用按地址匹配的字典方案。
    /// 房间在单线程中顺序调用 PollEvents()，OnConnectionRequest 与 OnPeerConnected 在
    /// 同一轮询周期内以 FIFO 顺序处理，不存在跨连接错位的竞态条件。
    /// 保留 ConcurrentQueue 以保证线程安全。</summary>
    private readonly ConcurrentQueue<string> _acceptedKeys = new();

    /// <summary>本房间的所有 UnitPawn。</summary>
    private readonly List<UnitPawn> _roomPawns = [];

    /// <summary>网络实体 ID 到战斗世界领域单位的映射，与 UnitPawn 同 ID 互绑。</summary>
    private readonly Dictionary<ushort, BattleUnit> _battleUnitByNetId = [];

    /// <summary>网络实体 ID 到 UnitPawn 的映射，移动桥与投影器定位载体用。</summary>
    private readonly Dictionary<ushort, UnitPawn> _pawnByNetId = [];

    /// <summary>房间网络实体，房间级战斗状态载体；整帧事件日志经传输层可靠通道外送，不经本实体承载。房间线程首帧初始化时填充。</summary>
    private BattleRoomEntity? _roomEntity;

    /// <summary>playerId 到其专属 Pawn 的映射，控制器绑定用；房间线程首帧迁移时填充。</summary>
    private readonly Dictionary<string, UnitPawn> _pawnByPlayerId = [];

    /// <summary>同阵营玩家出生点间距，大于两倍碰撞半径避免重叠。</summary>
    private const float SpawnSpacing = 3f;

    /// <summary>本房间的战斗世界，面向 BattleScene 具体类，不依赖网络载体与配置仓库。</summary>
    private readonly BattleScene _battleScene;

    /// <summary>本房间副本的阵营关系函数，AI 决策与战斗世界共用。</summary>
    private readonly CampRelationResolver _campRelations;

    /// <summary>本房间选中的副本键，来自 Store 房间配置，服务端据此生成敌人。</summary>
    private readonly string _dungeonKey;

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

    /// <summary>是否有任意活跃的客户端连接，断线保留实体的玩家不计入。</summary>
    public bool HasActiveConnections => !_peerToPlayerId.IsEmpty;

    /// <summary>仅用于调试或测试，不应在运行时由外部线程访问。</summary>
    internal UnitPawn[] GetPawnsSnapshot() => [.. _roomPawns];

    /// <summary>房间服务器是否正在运行。</summary>
    public bool IsRunning => _running;

    /// <summary>客户端连接事件。参数：peer ID。</summary>
    public event Action<int>? OnClientConnected;

    /// <summary>客户端断开事件。参数：peer ID。</summary>
    public event Action<int>? OnClientDisconnected;

    /// <summary>房间无任何活跃连接事件，房间线程触发，消费方负责在线程边界外执行销毁。</summary>
    public event Action<string>? RoomEmpty;

    /// <param name="port">监听端口</param>
    /// <param name="roomId">房间标识</param>
    /// <param name="loggerFactory">日志工厂，供 BattleRoomServer 与子组件创建日志器</param>
    /// <param name="config">战斗侧配置切片，连接密钥。</param>
    /// <param name="stateStore">大厅级状态存储，房间线程用于自取初始化数据与成员校验。</param>
    public BattleRoomServer(int port, string roomId, ILoggerFactory loggerFactory,
        BattleServerConfig config, IGameStateStore stateStore) {
        Port = port;
        RoomId = roomId;
        _logger = loggerFactory.CreateLogger<BattleRoomServer>();
        _connectionKey = config.ConnectionKey;
        _stateStore = stateStore;
        _dungeonKey = DungeonRegistry.Instance.GetByKey(stateStore.GetRoomConfig(roomId)?.DungeonKey)?.DungeonKey
            ?? throw new InvalidOperationException(
                $"Room '{roomId}' references unknown dungeon key.");
        _campRelations = DungeonRegistry.Instance.GetRelations(_dungeonKey);
        var movementScene = new PhysicsMovementScene(DungeonRegistry.Instance.GetMovementLayout(_dungeonKey));
        _battleScene = new BattleScene(_campRelations, movementScene, logger: loggerFactory.CreateLogger<BattleScene>());

        var typesMap = EntityTypesRegistry.EntityTypesMap;
        EntityManager = new ServerEntityManager(
            typesMap,
            NetworkDefaults.PacketHeader,
            framesPerSecond: FramesPerSecond,
            sendRate: ServerSendRate.EqualToFPS);

        _netManager = new NetManager(this);
    }

    /// <summary>
    /// 启动房间服务器：启动网络与独立线程主循环。
    /// 根实体创建、战斗引擎创建与准备期单位迁移均在房间线程首帧执行，
    /// 保证 EntityManager 的所有操作收敛到单一线程。
    /// </summary>
    public void Start() {
        _netManager.Start(Port);

        _running = true;
        _loopThread = new Thread(RunLoop) {
            Name = $"Room-{RoomId}",
            IsBackground = true
        };
        _loopThread.Start();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room '{RoomId}' started on port {Port} (thread: {ThreadName})",
                RoomId, Port, _loopThread.Name);
    }

    /// <summary>
    /// 等待房间线程完成首帧初始化，根实体、战斗世界与单位迁移。
    /// 配合 StartRoomBattle：初始化完成后才广播重定向，保证客户端连入时
    /// 房间已就绪。返回 false 表示等待超时。
    /// </summary>
    public bool WaitUntilInitialized(TimeSpan timeout) => _initialized.Wait(timeout);

    /// <summary>
    /// 停止房间服务器：先等房间线程退出，再清理全部会话与重连数据并关闭网络。
    /// 应由大厅线程调用，本方法会 Join 房间线程。
    /// </summary>
    public void Stop() {
        _running = false;

        // 先等待房间线程退出，再清理共享状态，避免大厅线程与房间线程并发访问
        _loopThread?.Join(TimeSpan.FromSeconds(3));
        _netManager.Stop();

        // 房间线程已退出，此时取消 Pawn 输入回调并移除战斗世界注册才是线程安全的
        foreach (var pawn in _roomPawns) {
            pawn.InputHandler = null;
            if (_battleUnitByNetId.TryGetValue(pawn.Id, out var unit))
                _battleScene.RemoveUnit(unit);
        }

        // 销毁全部保留实体，断线玩家实体随房间销毁一并清理，房间线程已退出
        CleanupAllSessions();

        // 释放初始化信号，不再有等待方
        _initialized.Dispose();

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("Room '{RoomId}' stopped on port {Port}", RoomId, Port);
    }

    /// <summary>
    /// 房间服务器主循环，独立线程：首帧初始化后轮询网络事件并驱动
    /// EntityManager.Update()。AI 决策与战斗推进经 BattleLoop LocalSingleton
    /// 收编进逻辑 tick 生命周期，时间由 LES accumulator 按真实时间统一管理。
    /// </summary>
    private void RunLoop() {
        // 首帧初始化：根实体、战斗世界与准备期单位迁移全部在房间线程完成
        try {
            InitializeFromStore();
            InitializeSucceeded = true;
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[RoomId: {RoomId}] Initialization failed.", RoomId);
            // 初始化失败不投递 RoomEmpty，由 StartRoomBattle 检查标志后同步清理
            return;
        }
        finally {
            // 即使初始化失败也放行，避免大厅线程 WaitUntilInitialized 无限等待
            _initialized.Set();
        }

        // 房间一经初始化即开战：阶段机 Waiting → Running 并写入 BattlePhase 载体。
        // 战斗只允许在房间线程启动，与 Tick / EntityManager.Update 保持线程所有权一致；
        // 大厅 StartRoomBattle 等待 _initialized 后才广播重定向，
        // 客户端连入时 BattlePhase 已为 Running，技能请求不会被阶段校验拒绝。
        try {
            StartBattle();
        }
        catch (Exception ex) {
            _logger.LogError(ex, "[RoomId: {RoomId}] StartBattle failed.", RoomId);
        }

        int consecutiveFailures = 0;
        while (_running) {
            try {
                // 网络事件收包入队；输入应用、实体更新、战斗推进与状态发送
                // 全部由 EntityManager.Update() 在逻辑 tick 内驱动。
                // Sleep 仅控制轮询节奏，不参与逻辑计时；tick 频率由 LES accumulator 保证。
                _netManager.PollEvents();
                EntityManager.Update();
                consecutiveFailures = 0;
            }
            catch (Exception ex) {
                consecutiveFailures++;
                _logger.LogError(ex, "[RoomId: {RoomId}] Unhandled exception in room tick.", RoomId);
                if (consecutiveFailures >= MaxConsecutiveTickFailures) {
                    if (_logger.IsEnabled(LogLevel.Critical))
                        _logger.LogCritical("[RoomId: {RoomId}] Too many consecutive tick failures, stopping room.", RoomId);
                    break;
                }
            }
            Thread.Sleep(1);
        }

        // 连续失败退出：投递空房事件由大厅清理循环销毁，避免带病房间残留。
        // 正常退出由 Stop() 置位 _running 触发，清理已由大厅线程完成。
        if (_running) {
            _running = false;
            RoomEmpty?.Invoke(RoomId);
        }
    }
}
