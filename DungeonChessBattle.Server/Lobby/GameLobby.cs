using System.Collections.Concurrent;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅模块，负责房间服务器的生命周期管理（创建、注册、查找、销毁）。
/// 不再持有房间内部数据（Pawns、Entities 等），所有数据所有权归 RoomEntityServer。
/// 线程安全：ConcurrentDictionary + 端口池仅大厅线程操作。
/// </summary>
public class GameLobby(ILoggerFactory loggerFactory) {
    private readonly ILogger<GameLobby> _logger = loggerFactory.CreateLogger<GameLobby>();
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    /// <summary>房间服务器注册表（线程安全）</summary>
    private readonly ConcurrentDictionary<string, RoomEntityServer> _roomServers = new();

    // 端口池：从 10171 开始递增分配（10170 留给大厅）
    private int _nextPort = 10171;
    private readonly ConcurrentQueue<int> _portPool = new();

    /// <summary>当前所有房间服务器（快照）</summary>
    public ICollection<RoomEntityServer> RoomServers => _roomServers.Values;

    // ── 端口管理 ──────────────────────────────────────────

    private int AllocatePort() {
        if (_portPool.TryDequeue(out int port))
            return port;
        return _nextPort++;
    }

    private void RecyclePort(int port) {
        _portPool.Enqueue(port);
    }

    // ── 房间服务器管理 ────────────────────────────────────

    /// <summary>
    /// 为指定 roomId 创建独立的 RoomEntityServer 并启动其线程。
    /// 如果房间已存在则返回现有实例。
    /// </summary>
    public RoomEntityServer CreateRoom(string roomId) {
        return _roomServers.GetOrAdd(roomId, _ => {
            int port = AllocatePort();
            var server = new RoomEntityServer(port, roomId, _loggerFactory.CreateLogger<RoomEntityServer>());
            server.Start();
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Lobby] Room '{RoomId}' created on port {Port}", roomId, port);
            return server;
        });
    }

    /// <summary>
    /// 获取房间服务器（如果存在）。
    /// </summary>
    public RoomEntityServer? GetRoom(string roomId) {
        _roomServers.TryGetValue(roomId, out var server);
        return server;
    }

    /// <summary>
    /// 获取或创建房间服务器，返回 (server, port)。
    /// </summary>
    public (RoomEntityServer server, int port) EnsureRoomServer(string roomId) {
        var server = CreateRoom(roomId);
        return (server, server.Port);
    }

    /// <summary>
    /// 移除并停止房间服务器。
    /// </summary>
    public bool RemoveRoom(string roomId) {
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
    /// 使用循环 TryRemove 确保清空时刻之后新加入的房间也被停止。
    /// </summary>
    public void StopAll() {
        while (!_roomServers.IsEmpty) {
            foreach (var (roomId, server) in _roomServers) {
                if (_roomServers.TryRemove(roomId, out _)) {
                    server.Stop();
                    RecyclePort(server.Port);
                }
            }
        }
    }

    // ── CLI ───────────────────────────────────────────────

    public void ListRooms() {
        foreach (var (id, server) in _roomServers) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("  {RoomId}: Peers={PeerCount}, Port={Port}, Running={Running}",
                    id, server.PeerCount, server.Port, server.IsRunning);
        }
    }

    /// <summary>
    /// 阻塞式交互 CLI 循环。
    /// </summary>
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
                    Console.WriteLine("  create <id>  |  remove <id>  |  rooms  |  status  |  exit");
                    break;
                case "status":
                    Console.WriteLine($"  Uptime: {getUptime():hh\\:mm\\:ss}, Clients: {getPeerCount()}");
                    break;
                case "rooms":
                    ListRooms();
                    break;
                case "create":
                    if (parts.Length >= 2) {
                        CreateRoom(parts[1]);
                        Console.WriteLine($"Room '{parts[1]}' created.");
                    }
                    break;
                case "remove":
                    if (parts.Length >= 2) {
                        RemoveRoom(parts[1]);
                        Console.WriteLine($"Room '{parts[1]}' removed.");
                    }
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
