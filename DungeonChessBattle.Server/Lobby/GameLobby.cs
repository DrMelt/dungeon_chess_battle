using System.Collections.Concurrent;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅模块，负责房间服务器的生命周期管理（创建、注册、查找、销毁）。
/// 不再持有房间内部数据（Pawns、Entities 等），所有数据所有权归 RoomEntityServer。
/// 线程安全：ConcurrentDictionary + 端口池仅大厅线程操作。
/// 支持房间密码验证。
/// </summary>
public class GameLobby(ILoggerFactory loggerFactory) {
    private readonly ILogger<GameLobby> _logger = loggerFactory.CreateLogger<GameLobby>();
    private readonly ILoggerFactory _loggerFactory = loggerFactory;

    /// <summary>房间服务器注册表（线程安全）</summary>
    private readonly ConcurrentDictionary<string, RoomEntityServer> _roomServers = new();

    /// <summary>房间密码字典（线程安全）。null 表示无密码房间。</summary>
    private readonly ConcurrentDictionary<string, string?> _roomPasswords = new();

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
    /// <param name="roomId">房间标识</param>
    /// <param name="password">房间密码。null 表示无密码房间</param>
    public RoomEntityServer CreateRoom(string roomId, string? password = null) {
        return _roomServers.GetOrAdd(roomId, _ => {
            int port = AllocatePort();
            var server = new RoomEntityServer(port, roomId, _loggerFactory.CreateLogger<RoomEntityServer>());
            server.Start();
            _roomPasswords[roomId] = password;
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("[Lobby] Room '{RoomId}' created on port {Port}, HasPassword={HasPwd}",
                    roomId, port, password != null);
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
    /// 验证房间密码。
    /// </summary>
    /// <param name="roomId">房间标识</param>
    /// <param name="password">客户端提供的密码（null 或空字符串表示无密码）</param>
    /// <returns>密码匹配或房间无密码时返回 true</returns>
    public bool ValidateRoomPassword(string roomId, string? password) {
        if (!_roomPasswords.TryGetValue(roomId, out var storedPassword))
            return false;  // 房间不存在

        // 存储的密码为 null 表示无密码房间，任何密码通过
        if (storedPassword == null)
            return true;

        // 有密码房间：必须提供匹配密码
        return storedPassword == password;
    }

    /// <summary>
    /// 移除并停止房间服务器。
    /// </summary>
    public bool RemoveRoom(string roomId) {
        _roomPasswords.TryRemove(roomId, out _);
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
        _roomPasswords.Clear();
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
                _logger.LogInformation("  {RoomId}: Peers={PeerCount}, Port={Port}, Running={Running}, HasPassword={HasPwd}",
                    id, server.PeerCount, server.Port, server.IsRunning, _roomPasswords.TryGetValue(id, out var p) && p != null);
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
                    Console.WriteLine("  create <id> [password]  |  remove <id>  |  rooms  |  status  |  exit");
                    break;
                case "status":
                    Console.WriteLine($"  Uptime: {getUptime():hh\\:mm\\:ss}, Clients: {getPeerCount()}");
                    break;
                case "rooms":
                    ListRooms();
                    break;
                case "create":
                    if (parts.Length >= 2) {
                        string? pwd = parts.Length >= 3 ? parts[2] : null;
                        CreateRoom(parts[1], pwd);
                        Console.WriteLine($"Room '{parts[1]}' created, password: {(pwd != null ? "set" : "none")}.");
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