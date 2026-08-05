using System.Collections.Concurrent;
using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Server.Network;
using LiteNetLib;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅模块，负责房间服务器的生命周期管理（创建、注册、查找、销毁）。
/// 准备阶段（选单位等）在大厅 JSON 协议上完成，战斗开始时才创建 BattleRoomServer。
/// 线程安全：ConcurrentDictionary + 端口池仅大厅线程操作。
/// 支持房间密码验证和招募板配置。
/// </summary>
public class GameLobby(ILoggerFactory loggerFactory) {
    private readonly ILogger<GameLobby> _logger = loggerFactory.CreateLogger<GameLobby>();
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    /// <summary>房间服务器注册表（线程安全）。准备阶段房间不在此表中。</summary>
    private readonly ConcurrentDictionary<string, BattleRoomServer> _roomServers = new();

    /// <summary>房间密码字典（线程安全）。null 表示无密码房间。</summary>
    private readonly ConcurrentDictionary<string, string?> _roomPasswords = new();

    /// <summary>房间配置注册表（招募板使用），用于存储 GameRoom 的招募板配置信息。</summary>
    private readonly ConcurrentDictionary<string, GameRoom> _roomConfigs = new();

    /// <summary>准备阶段单位数据表：房间ID 映射到（单位名, 阵营字符串）列表。</summary>
    private readonly ConcurrentDictionary<string, List<(string UnitName, string Camp)>> _prepareUnits = new();

    /// <summary>房间 → 该房间内的所有大厅 peer（用于准备阶段广播）</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, NetPeer>> _roomPeers = new();

    // 端口池：从 10171 开始递增分配（10170 留给大厅）
    private int _nextPort = 10171;
    private readonly ConcurrentQueue<int> _portPool = new();

    /// <summary>当前所有房间服务器（快照）</summary>
    public ICollection<BattleRoomServer> RoomServers => _roomServers.Values;

    /// <summary>
    /// 从端口池获取或递增分配一个房间端口。
    /// </summary>
    private int AllocatePort() {
        if (_portPool.TryDequeue(out int port))
            return port;
        return _nextPort++;
    }

    /// <summary>
    /// 回收房间端口到端口池。
    /// </summary>
    private void RecyclePort(int port) {
        _portPool.Enqueue(port);
    }

    /// <summary>
    /// 注册房间（仅存储配置和密码，不创建独立服务器）。
    /// 用于准备阶段的 create_room。如果房间已存在则返回 false。
    /// </summary>
    public bool RegisterRoom(string roomId, string? password, GameRoom config) {
        if (_roomConfigs.ContainsKey(roomId))
            return false;

        _roomPasswords[roomId] = password;
        _roomConfigs[roomId] = config;
        _prepareUnits[roomId] = [];
        _roomPeers[roomId] = [];

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' registered (prepare), HasPassword={HasPwd}, Title={Title}",
                roomId, password != null, config.Title);
        return true;
    }

    /// <summary>
    /// 开始战斗：创建 BattleRoomServer，迁移准备期单位数据，然后重定向客户端。
    /// </summary>
    public BattleRoomServer StartRoomBattle(string roomId) {
        // 分配端口并创建 BattleRoomServer
        int port = AllocatePort();
        var server = new BattleRoomServer(port, roomId, _loggerFactory.CreateLogger<BattleRoomServer>());
        server.Start();

        _roomServers[roomId] = server;

        // 迁移准备期单位数据到 LES
        if (_prepareUnits.TryGetValue(roomId, out var units)) {
            foreach (var (unitName, camp) in units) {
                var spawnPos = camp == CampConstants.CampA
                    ? new System.Numerics.Vector2(0, 0)
                    : new System.Numerics.Vector2(5, 0);
                server.CreatePawnEntity(unitName, camp, spawnPos);
            }
        }

        // 更新招募板状态
        if (_roomConfigs.TryGetValue(roomId, out var config)) {
            config.Status = RoomStatus.InProgress;
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' battle started on port {Port}, units={UnitCount}",
                roomId, port, units?.Count ?? 0);

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
    /// 检查房间是否已注册（准备阶段或战斗中）。
    /// </summary>
    public bool RoomExists(string roomId) {
        return _roomConfigs.ContainsKey(roomId);
    }

    /// <summary>
    /// 获取房间配置（招募板信息）。
    /// </summary>
    public GameRoom? GetRoomConfig(string roomId) {
        _roomConfigs.TryGetValue(roomId, out var config);
        return config;
    }

    /// <summary>
    /// 在大厅准备阶段添加单位。
    /// </summary>
    public bool AddPrepareUnit(string roomId, string unitName, string camp) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return false;

        units.Add((unitName, camp));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' add unit: {UnitName}, camp={Camp}, total={Count}",
                roomId, unitName, camp, units.Count);

        return true;
    }

    /// <summary>
    /// 在大厅准备阶段移除单位。
    /// </summary>
    public bool RemovePrepareUnit(string roomId, string unitName, string camp) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return false;

        var removed = units.RemoveAll(u => u.UnitName == unitName && u.Camp == camp) > 0;

        if (removed && _logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' remove unit: {UnitName}, camp={Camp}, remaining={Count}",
                roomId, unitName, camp, units.Count);

        return removed;
    }

    /// <summary>
    /// 获取准备阶段单位列表。
    /// </summary>
    public List<(string UnitName, string Camp)> GetPrepareUnits(string roomId) {
        if (_prepareUnits.TryGetValue(roomId, out var units))
            return [.. units];
        return [];
    }

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

    /// <summary>
    /// 获取所有有效房间的招募板列表，按创建时间倒序排列。
    /// </summary>
    public List<RoomListing> GetRoomListings() {
        return [.. _roomConfigs
            .Where(kvp => kvp.Value.Status != RoomStatus.Finished)
            .Select(kvp => RoomListing.FromGameRoom(kvp.Value))
            .OrderByDescending(r => r.CreatedAt)];
    }

    /// <summary>
    /// 更新房间的招募板状态。
    /// </summary>
    public void UpdateRoomStatus(string roomId, RoomStatus status) {
        if (_roomConfigs.TryGetValue(roomId, out var config)) {
            config.Status = status;
        }
    }

    /// <summary>
    /// 更新房间当前玩家数。
    /// </summary>
    public void UpdatePlayerCount(string roomId, int count) {
        if (_roomConfigs.TryGetValue(roomId, out var config)) {
            config.CurrentPlayers = count;
        }
    }

    /// <summary>
    /// 验证房间密码。
    /// </summary>
    public bool ValidateRoomPassword(string roomId, string? password) {
        if (!_roomPasswords.TryGetValue(roomId, out var storedPassword))
            return false;

        if (storedPassword == null)
            return true;

        return storedPassword == password;
    }

    /// <summary>
    /// 移除并停止房间服务器。
    /// </summary>
    public bool RemoveRoom(string roomId) {
        _roomPasswords.TryRemove(roomId, out _);
        _roomConfigs.TryRemove(roomId, out _);
        _prepareUnits.TryRemove(roomId, out _);
        _roomPeers.TryRemove(roomId, out _);

        if (_roomServers.TryRemove(roomId, out var server)) {
            server.Stop();
            RecyclePort(server.Port);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Lobby] Room '{RoomId}' removed (port {Port} recycled)", roomId, server.Port);
            return true;
        }
        return false;
    }

    /// <summary>
    /// 停止所有房间服务器。
    /// </summary>
    public void StopAll() {
        _roomPasswords.Clear();
        _roomConfigs.Clear();
        _prepareUnits.Clear();
        _roomPeers.Clear();
        while (!_roomServers.IsEmpty) {
            foreach (var (roomId, server) in _roomServers) {
                if (_roomServers.TryRemove(roomId, out _)) {
                    server.Stop();
                    RecyclePort(server.Port);
                }
            }
        }
    }

    /// <summary>
    /// 输出所有房间的基本信息（用于控制台命令 rooms）。
    /// </summary>
    public void ListRooms() {
        foreach (var (id, config) in _roomConfigs) {
            bool isBattle = _roomServers.ContainsKey(id);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("  {RoomId}: Title={Title}, Phase={Phase}, HasPassword={HasPwd}",
                    id, config.Title, isBattle ? "Battle" : "Prepare",
                    _roomPasswords.TryGetValue(id, out var p) && p != null);
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
