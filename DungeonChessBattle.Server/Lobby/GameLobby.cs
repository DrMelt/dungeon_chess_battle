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

    /// <summary>准备阶段单位数据表：房间ID 映射到（单位名, 阵营, 玩家名）列表。</summary>
    private readonly ConcurrentDictionary<string, List<(string UnitName, string Camp, string PlayerName)>> _prepareUnits = new();

    /// <summary>房间 → 该房间内的所有大厅 peer（用于准备阶段广播）</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, NetPeer>> _roomPeers = new();

    /// <summary>房主玩家名表：房间ID → 房主 displayName。</summary>
    private readonly ConcurrentDictionary<string, string> _roomHosts = new();

    /// <summary>玩家准备状态表：房间ID → (玩家名 → 是否已准备)。</summary>
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, bool>> _roomReadyStates = new();

    /// <summary>房间内玩家的 peer 归属表：peerId → (房间ID, 玩家名)，用于断线清理与身份校验。</summary>
    private readonly ConcurrentDictionary<int, (string RoomId, string PlayerName)> _peerPlayers = new();

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
        _roomReadyStates[roomId] = [];

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' registered (prepare), HasPassword={HasPwd}, Title={Title}",
                roomId, password != null, config.Title);
        return true;
    }

    /// <summary>
    /// 设置房间房主玩家名（由服务端解析的 displayName 写入，不信任客户端配置）。
    /// </summary>
    public void SetRoomHost(string roomId, string hostName) {
        _roomHosts[roomId] = hostName;
        // 将房主登记为房间成员（准备状态默认未准备，房主的准备状态不参与全员判定）
        var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
        states.TryAdd(hostName, false);
    }

    /// <summary>
    /// 登记玩家为房间准备阶段成员（默认未准备）。
    /// 房主也已由 SetRoomHost 登记 ready 状态，此处仅登记 peer 归属。
    /// </summary>
    public void RegisterRoomPlayer(string roomId, string playerName, NetPeer peer) {
        var states = _roomReadyStates.GetOrAdd(roomId, _ => new ConcurrentDictionary<string, bool>());
        states.TryAdd(playerName, false);
        _peerPlayers[peer.Id] = (roomId, playerName);
    }

    /// <summary>
    /// 设置房间内玩家准备状态（仅限非房主；房主身份不参与准备判定）。
    /// </summary>
    public void SetPlayerReady(string roomId, string playerName, bool ready) {
        if (_roomHosts.TryGetValue(roomId, out var hostName) && hostName == playerName)
            return;

        if (_roomReadyStates.TryGetValue(roomId, out var states))
            states[playerName] = ready;
    }

    /// <summary>
    /// 获取房间准备状态快照：(房主名, 副本名, 玩家(名, 准备标志)列表)。
    /// </summary>
    public (string HostName, string DungeonName, List<(string PlayerName, bool Ready)> Players) GetRoomState(string roomId) {
        string hostName = _roomHosts.TryGetValue(roomId, out var host) ? host : "";
        string dungeonName = _roomConfigs.TryGetValue(roomId, out var config) ? config.DungeonName : "";
        var players = new List<(string, bool)>();
        if (_roomReadyStates.TryGetValue(roomId, out var states)) {
            foreach (var kv in states)
                players.Add((kv.Key, kv.Value));
        }
        return (hostName, dungeonName, players);
    }

    /// <summary>
    /// 判断房间内除房主外的所有玩家是否都已准备。
    /// 无其他成员时视为已满足（本地/单人房间可直接开始）。
    /// </summary>
    public bool IsAllOthersReady(string roomId) {
        if (!_roomReadyStates.TryGetValue(roomId, out var states))
            return false;

        if (_roomHosts.TryGetValue(roomId, out var hostName)) {
            foreach (var kv in states) {
                if (kv.Key == hostName)
                    continue;
                if (!kv.Value)
                    return false;
            }
            return true;
        }

        // 无房主记录时退化为全部成员检查
        foreach (var kv in states) {
            if (!kv.Value)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 判断指定 peer 是否为指定房间的房主（基于 peer 归属表）。
    /// </summary>
    public bool IsPeerRoomHost(int peerId, string roomId) {
        if (!_peerPlayers.TryGetValue(peerId, out var entry))
            return false;
        if (entry.RoomId != roomId)
            return false;
        return _roomHosts.TryGetValue(roomId, out var host) && entry.PlayerName == host;
    }

    /// <summary>
    /// 移除房间内玩家（peer 断线清理）：从成员与身份表移除，返回所属房间 ID（用于后续广播）。
    /// </summary>
    public string? RemovePlayerFromRoom(int peerId) {
        if (!_peerPlayers.TryRemove(peerId, out var entry))
            return null;

        if (_roomReadyStates.TryGetValue(entry.RoomId, out var states)) {
            states.TryRemove(entry.PlayerName, out _);
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Lobby] Player '{Player}' removed from room '{RoomId}' (disconnected)",
                    entry.PlayerName, entry.RoomId);
        }
        return entry.RoomId;
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
            foreach (var (unitName, camp, _) in units) {
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
    /// 在大厅准备阶段添加单位（归属玩家由服务端经 peer 表解析）。
    /// </summary>
    public bool AddPrepareUnit(string roomId, string unitName, string camp, string playerName) {
        if (!_prepareUnits.TryGetValue(roomId, out var units))
            return false;

        units.Add((unitName, camp, playerName));

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("[Lobby] Room '{RoomId}' add unit: {UnitName}, camp={Camp}, player={PlayerName}, total={Count}",
                roomId, unitName, camp, playerName, units.Count);

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
    public List<(string UnitName, string Camp, string PlayerName)> GetPrepareUnits(string roomId) {
        if (_prepareUnits.TryGetValue(roomId, out var units))
            return [.. units];
        return [];
    }

    /// <summary>
    /// 解析指定 peer 在房间内登记的玩家名（服务器权威身份，不信任客户端提交）。
    /// </summary>
    public string? GetPlayerNameForPeer(int peerId) {
        if (_peerPlayers.TryGetValue(peerId, out var entry))
            return entry.PlayerName;
        return null;
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
        _roomHosts.TryRemove(roomId, out _);
        _roomReadyStates.TryRemove(roomId, out _);
        // 清理 peerPlayers 中属于该房间的条目
        foreach (var kv in _peerPlayers) {
            if (kv.Value.RoomId == roomId)
                _peerPlayers.TryRemove(kv.Key, out _);
        }

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
        _roomHosts.Clear();
        _roomReadyStates.Clear();
        _peerPlayers.Clear();
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
