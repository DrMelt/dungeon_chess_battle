using System.Collections.Concurrent;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Battle;
using DungeonChessBattle.Server.Domain.Settings;
using DungeonChessBattle.Server.State;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Host;

/// <summary>
/// 房间服务器生命周期管理器（协调层）。
/// 负责战斗房间服务器的创建、查找、销毁与端口分配，以及空房清理。
/// 战斗房间内实体同步与战斗逻辑由 <see cref="BattleRoomServer"/> 承担；
/// 大厅业务（准备、组队、快照）由 Server.Lobby 的 <see cref="DungeonChessBattle.Server.Lobby.GameLobby"/> 承担。
/// 大厅级状态数据由 <see cref="IGameStateStore"/> 持有，本类不直接存储业务状态。
/// 线程所有权：房间线程通过 BattleRoomServer.RoomEmpty 事件仅向队列投递 roomId，
/// 由后台清理循环 <see cref="ProcessPendingRoomCleanups"/> 消费执行销毁。
/// </summary>
/// <param name="loggerFactory">日志工厂。</param>
/// <param name="stateStore">大厅级状态存储。</param>
/// <param name="config">服务器配置（房间端口池起点）。</param>
public sealed class RoomServerManager(ILoggerFactory loggerFactory, IGameStateStore stateStore, ServerConfig config) {
    private readonly ILogger<RoomServerManager> _logger = loggerFactory.CreateLogger<RoomServerManager>();
    private readonly ILoggerFactory _loggerFactory = loggerFactory;
    private readonly IGameStateStore _stateStore = stateStore;
    private readonly ServerConfig _config = config;

    /// <summary>房间服务器注册表（线程安全）。准备阶段房间不在此表中。</summary>
    private readonly ConcurrentDictionary<string, BattleRoomServer> _roomServers = new();

    /// <summary>
    /// 空房间投递队列：房间线程在无活跃连接且初始化完成后投递 roomId，
    /// 后台清理循环消费并执行移除。保证 _roomServers / 端口池仅在
    /// 清理循环内被修改（线程所有权）。
    /// </summary>
    private readonly ConcurrentQueue<string> _pendingEmptyRooms = new();

    // 端口池：从配置的 FirstRoomPort 开始递增分配（大厅端口之后）
    private int _nextPort = config.FirstRoomPort;
    private readonly ConcurrentQueue<int> _portPool = new();

    /// <summary>当前所有房间服务器（快照）</summary>
    public ICollection<BattleRoomServer> RoomServers => _roomServers.Values;

    /// <summary>
    /// 从端口池获取或递增分配一个房间端口。仅协调线程调用。
    /// </summary>
    private int AllocatePort() {
        if (_portPool.TryDequeue(out int port))
            return port;
        return _nextPort++;
    }

    /// <summary>
    /// 回收房间端口到端口池。仅协调线程调用。
    /// </summary>
    private void RecyclePort(int port) {
        _portPool.Enqueue(port);
    }

    /// <summary>
    /// 消费空房间投递队列并执行房间移除。由协调线程每轮循环调用。
    /// </summary>
    public void ProcessPendingRoomCleanups() {
        while (_pendingEmptyRooms.TryDequeue(out string? roomId) && roomId != null)
            RemoveRoom(roomId);
    }

    /// <summary>
    /// 房间无活跃连接事件处理：仅入队，由协调线程消费执行移除。
    /// 不做 ContainsKey 预检：事件可能发生在 _roomServers 注册完成前
    /// （初始化完成后、注册前客户端连入又断开），预检会丢弃事件导致
    /// 空房间永久泄漏。RemoveRoom 本身幂等，重复入队无害。
    /// </summary>
    private void OnRoomEmptied(string roomId) {
        _pendingEmptyRooms.Enqueue(roomId);

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' queued for removal (no active connections).", roomId);
    }

    // ─── 战斗房间生命周期 ───

    /// <summary>
    /// 开始战斗：创建 BattleRoomServer 并等待其完成首帧初始化。
    /// 初始化（根实体、Logic 房间、单位迁移）全部在房间线程完成；
    /// 本方法仅执行生命周期控制，不触碰 EntityManager。
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
    /// 移除并停止房间服务器，同时清理 store 中的房间状态。
    /// 由后台清理循环调用（等待初始化成功后房间线程已可安全 Join）。
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
}
