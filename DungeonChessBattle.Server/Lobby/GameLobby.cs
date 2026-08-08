using System.Collections.Concurrent;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Network;
using DungeonChessBattle.Server.Settings;
using DungeonChessBattle.Server.Stores;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅协调者，负责战斗房间服务器的生命周期管理（创建、查找、销毁、端口分配）。
/// 大厅级状态数据（房间配置、密码、玩家准备状态、准备单位等）统一由
/// <see cref="IGameStateStore"/> 持有，本类不再直接存储状态。
/// 准备阶段（选单位等）在大厅 JSON 协议上完成，战斗开始时才创建 BattleRoomServer。
/// 线程所有权：本类的可变状态（房间服务器注册表、端口池、peer 引用表、
/// 空房间投递队列）仅大厅线程（Lobby-Poll）操作。房间线程通过
/// BattleRoomServer.RoomEmpty 事件仅向队列投递 roomId，由大厅线程
/// <see cref="ProcessPendingRoomCleanups"/> 消费执行销毁。
/// </summary>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="stateStore">大厅级状态存储。</param>
/// <param name="config">服务器配置（房间端口池起点）。</param>
public class GameLobby(ILoggerFactory loggerFactory, IGameStateStore stateStore, ServerConfig config) {
    private readonly ILogger<GameLobby> _logger = loggerFactory.CreateLogger<GameLobby>();
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly ServerConfig _config = config;

    /// <summary>房间服务器注册表（线程安全）。准备阶段房间不在此表中。</summary>
    private readonly ConcurrentDictionary<string, BattleRoomServer> _roomServers = new();

    /// <summary>房间 → 该房间内的所有大厅 peer（用于准备阶段广播）</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, NetPeer>> _roomPeers = new();

    /// <summary>
    /// 空房间投递队列：房间线程在无活跃连接且初始化完成后投递 roomId，
    /// 大厅线程每轮循环消费并执行移除。保证 _roomServers / 端口池仅在
    /// 大厅线程被修改（线程所有权）。
    /// </summary>
    private readonly ConcurrentQueue<string> _pendingEmptyRooms = new();

    // 端口池：从配置的 FirstRoomPort 开始递增分配（大厅端口之后）
    private int _nextPort = config.FirstRoomPort;
    private readonly ConcurrentQueue<int> _portPool = new();

    /// <summary>当前所有房间服务器（快照）</summary>
    public ICollection<BattleRoomServer> RoomServers => _roomServers.Values;

    /// <summary>
    /// 从端口池获取或递增分配一个房间端口。仅大厅线程调用。
    /// </summary>
    private int AllocatePort() {
        if (_portPool.TryDequeue(out int port))
            return port;
        return _nextPort++;
    }

    /// <summary>
    /// 回收房间端口到端口池。仅大厅线程调用。
    /// </summary>
    private void RecyclePort(int port) {
        _portPool.Enqueue(port);
    }

    /// <summary>
    /// 消费空房间投递队列并执行房间移除。由大厅线程每轮循环调用。
    /// </summary>
    public void ProcessPendingRoomCleanups() {
        while (_pendingEmptyRooms.TryDequeue(out string? roomId) && roomId != null)
            RemoveRoom(roomId);
    }

    /// <summary>
    /// 房间无活跃连接事件处理：仅入队，由大厅线程消费执行移除。
    /// 不做 ContainsKey 预检：事件可能发生在 _roomServers 注册完成前
    /// （初始化完成后、注册前客户端连入又断开），预检会丢弃事件导致
    /// 空房间永久泄漏。RemoveRoom 本身幂等，重复入队无害。
    /// </summary>
    private void OnRoomEmptied(string roomId) {
        _pendingEmptyRooms.Enqueue(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' queued for removal (no active connections).", roomId);
    }

    // ─── 房间 peer 引用管理（广播用；peer 对象属网络层，不入 store）───

    /// <summary>
    /// 将大厅 peer 注册到房间（用于准备阶段的广播）。
    /// </summary>
    public void RegisterPeerToRoom(string roomId, NetPeer peer) {
        var peers = _roomPeers.GetOrAdd(roomId, _ => new ConcurrentDictionary<int, NetPeer>());
        peers[peer.Id] = peer;

        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("[Lobby] Peer {PeerId} registered to room '{RoomId}'", peer.Id, roomId);
    }

    /// <summary>
    /// 从房间中移除大厅 peer。
    /// </summary>
    public void UnregisterPeerFromRoom(string roomId, int peerId) {
        if (_roomPeers.TryGetValue(roomId, out var peers)) {
            peers.TryRemove(peerId, out _);
            if (_logger.IsEnabled(LogLevel.Debug))
                _logger.LogDebug("[Lobby] Peer {PeerId} unregistered from room '{RoomId}'", peerId, roomId);
        }
    }

    /// <summary>
    /// 获取房间内所有大厅 peer。
    /// </summary>
    public IReadOnlyCollection<NetPeer> GetRoomPeers(string roomId) {
        if (_roomPeers.TryGetValue(roomId, out var peers))
            return [.. peers.Values];
        return [];
    }

    /// <summary>清理房间的全部 peer 引用。仅大厅线程调用。</summary>
    private void RemoveRoomPeers(string roomId) {
        _roomPeers.TryRemove(roomId, out _);
    }

    // ─── 战斗房间生命周期 ───

    /// <summary>
    /// 开始战斗：创建 BattleRoomServer 并等待其完成首帧初始化。
    /// 初始化（根实体、Logic 房间、单位迁移）全部在房间线程完成；
    /// 本方法仅在大厅线程执行生命周期控制，不触碰 EntityManager。
    /// </summary>
    public BattleRoomServer StartRoomBattle(string roomId) {
        // 分配端口并创建 BattleRoomServer
        int port = AllocatePort();
        var server = new BattleRoomServer(port, roomId,
            _loggerFactory.CreateLogger<BattleRoomServer>(),
            _loggerFactory.CreateLogger<GameLogicService>(),
            _config, _stateStore);
        server.Start();

        // 房间全部活跃连接断开后自动销毁（闭合 RoomEmpty 事件链；仅入队）
        server.RoomEmpty += OnRoomEmptied;

        // 等待首帧初始化完成，保证客户端连入时根实体已就绪
        if (!server.WaitUntilInitialized(TimeSpan.FromSeconds(10)))
            throw new InvalidOperationException($"Room '{roomId}' failed to initialize within timeout.");

        _roomServers[roomId] = server;

        // 更新招募板状态
        _stateStore.UpdateRoomStatus(roomId, RoomStatus.InProgress);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' battle started on port {Port}",
                roomId, port);

        return server;
    }

    /// <summary>
    /// 获取房间服务器（仅战斗中的房间有此数据）。
    /// </summary>
    public BattleRoomServer? GetRoomServer(string roomId) {
        _roomServers.TryGetValue(roomId, out var server);
        return server;
    }

    /// <summary>
    /// 移除并停止房间服务器，同时清理 store 中的房间状态与房间 peer 引用。
    /// 仅大厅线程调用（等待初始化成功后房间线程已可安全 Join）。
    /// </summary>
    public bool RemoveRoom(string roomId) {
        bool removed;
        if (_roomServers.TryRemove(roomId, out var server)) {
            server.RoomEmpty -= OnRoomEmptied;
            server.Stop();
            RecyclePort(server.Port);
            removed = true;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Lobby] Room '{RoomId}' removed (port {Port} recycled)", roomId, server.Port);
        }
        else {
            removed = false;
        }

        RemoveRoomPeers(roomId);
        _stateStore.RemoveRoomState(roomId);
        return removed;
    }

    /// <summary>
    /// 停止所有房间服务器并清空大厅状态。
    /// </summary>
    public void StopAll() {
        while (!_roomServers.IsEmpty) {
            foreach (var (roomId, server) in _roomServers) {
                if (_roomServers.TryRemove(roomId, out _)) {
                    server.RoomEmpty -= OnRoomEmptied;
                    server.Stop();
                    RecyclePort(server.Port);
                }
            }
        }
        _pendingEmptyRooms.Clear();
        _roomPeers.Clear();
        _stateStore.ClearAllState();
    }

    /// <summary>
    /// 输出所有房间的基本信息（用于控制台命令 rooms）。
    /// </summary>
    public void ListRooms() {
        foreach (var listing in _stateStore.ListActiveRooms()) {
            bool isBattle = _roomServers.ContainsKey(listing.RoomId);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("  {RoomId}: Title={Title}, Phase={Phase}, HasPassword={HasPwd}",
                    listing.RoomId, listing.Title, isBattle ? "Battle" : "Prepare", listing.HasPassword);
        }
    }

    /// <summary>
    /// 运行控制台交互循环，支持 help / status / rooms / exit 命令。
    /// </summary>
    /// <param name="getPeerCount">获取当前在线人数委托。</param>
    /// <param name="getUptime">获取服务运行时长委托。</param>
    public void RunConsoleLoop(Func<int> getPeerCount, Func<TimeSpan> getUptime) {
        while (true) {
            Console.Write("> ");
            var line = Console.ReadLine();
            if (line == null)
                break;
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            switch (parts[0].ToLowerInvariant()) {
                case "help":
                    Console.WriteLine("  rooms  |  status  |  exit");
                    break;
                case "status":
                    Console.WriteLine($"  Uptime: {getUptime():hh\\:mm\\:ss}, Clients: {getPeerCount()}");
                    break;
                case "rooms":
                    ListRooms();
                    break;
                case "exit":
                case "quit":
                    return;
                default:
                    Console.WriteLine($"Unknown: {parts[0]}");
                    break;
            }
        }
    }
}
