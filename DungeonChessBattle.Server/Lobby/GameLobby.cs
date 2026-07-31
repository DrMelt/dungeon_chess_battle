using System.Numerics;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅模块，负责房间/单位的 CRUD、实体缓存和交互式 CLI。
/// 实时化：完全使用 UnitPawn，不再依赖 UnitSyncEntity。
/// 多房间隔离：每个房间拥有独立的 RoomEntityServer。
/// </summary>
public class GameLobby {
    private readonly IServerBattleService _battleService;
    private readonly ILogger<GameLobby> _logger;
    private readonly ILoggerFactory _loggerFactory;

    private readonly Dictionary<string, BattleRoomEntity> _roomEntities = [];
    private readonly Dictionary<string, List<UnitPawn>> _roomPawns = [];
    private readonly Dictionary<string, RoomEntityServer> _roomServers = [];

    // 端口池：从 10171 开始递增分配（10170 留给大厅）
    private int _nextPort = 10171;
    private readonly Queue<int> _portPool = new();

    public IReadOnlyDictionary<string, BattleRoomEntity> Rooms => _roomEntities;
    public IReadOnlyDictionary<string, List<UnitPawn>> RoomPawns => _roomPawns;
    public IReadOnlyDictionary<string, RoomEntityServer> RoomServers => _roomServers;

    public GameLobby(IServerBattleService battleService, ILoggerFactory loggerFactory) {
        _battleService = battleService;
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<GameLobby>();
    }

    // ── 端口管理 ──────────────────────────────────────────

    private int AllocatePort() {
        if (_portPool.Count > 0)
            return _portPool.Dequeue();
        return _nextPort++;
    }

    private void RecyclePort(int port) {
        _portPool.Enqueue(port);
    }

    // ── 房间服务器管理 ────────────────────────────────────

    /// <summary>
    /// 为指定 roomId 创建独立的 RoomEntityServer，并在其中创建 BattleRoomEntity。
    /// </summary>
    public RoomEntityServer CreateRoomServer(string roomId) {
        if (_roomServers.TryGetValue(roomId, out var existing))
            return existing;

        int port = AllocatePort();
        var server = new RoomEntityServer(port, roomId, _loggerFactory.CreateLogger<RoomEntityServer>());
        server.Start();

        // 在房间 SEM 中创建 BattleRoomEntity
        var entity = server.EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = roomId;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{roomId}'.");

        _roomServers[roomId] = server;
        _roomEntities[roomId] = entity;
        _roomPawns[roomId] = [];

        // 同步在 Logic 层创建对应房间
        _battleService.CreateRoom(roomId);

        return server;
    }

    /// <summary>
    /// 获取房间的 ServerEntityManager（可能为 null）。
    /// </summary>
    public RoomEntityServer? GetRoomServer(string roomId) {
        _roomServers.TryGetValue(roomId, out var server);
        return server;
    }

    // ── Entity CRUD ───────────────────────────────────────

    /// <summary>
    /// 创建实时 UnitPawn 实体。
    /// </summary>
    public UnitPawn CreatePawnEntity(RoomEntityServer roomServer, string roomId, string unitName, byte camp, Vector2 spawnPos) {
        var entity = roomServer.EntityManager.AddEntity<UnitPawn>(e => {
            e.UnitName.Value = unitName;
            e.Camp.Value = camp;
            e.Position.Value = spawnPos;
        }) ?? throw new InvalidOperationException($"Failed to create UnitPawn for unit '{unitName}' in room '{roomId}'.");

        if (!_roomPawns.TryGetValue(roomId, out var list)) {
            list = [];
            _roomPawns[roomId] = list;
        }
        list.Add(entity);

        // 委托 Logic 层创建单位
        if (_battleService is GameLogicService logicService) {
            logicService.CreateUnit(roomId, unitName, camp);
        }

        return entity;
    }

    /// <summary>
    /// 获取指定房间的所有 UnitPawn（可能为空列表）。
    /// </summary>
    public IReadOnlyList<UnitPawn> GetRoomPawns(string roomId) {
        return _roomPawns.TryGetValue(roomId, out var list) ? list : [];
    }

    /// <summary>
    /// 根据 UnitPawn 查找其所属的房间 ID。
    /// </summary>
    public string? FindRoomIdByPawn(UnitPawn pawn) {
        foreach (var (roomId, pawns) in _roomPawns) {
            if (pawns.Contains(pawn))
                return roomId;
        }
        return null;
    }

    public bool RemoveRoom(string roomId) {
        _roomPawns.Remove(roomId);
        _roomEntities.Remove(roomId);

        if (_roomServers.TryGetValue(roomId, out var server)) {
            server.Stop();
            RecyclePort(server.Port);
            _roomServers.Remove(roomId);
        }

        _battleService.RemoveRoom(roomId);
        return true;
    }

    public void ListRooms() {
        foreach (var (id, room) in _roomEntities) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("  {RoomId}: Phase={Phase}, Pawns={PawnCount}, Port={Port}",
                    id, room.BattlePhase.Value,
                    _roomPawns.GetValueOrDefault(id)?.Count ?? 0,
                    _roomServers.GetValueOrDefault(id)?.Port);
        }
    }

    // ── 房间生命周期 ──────────────────────────────────────

    /// <summary>
    /// 当客户端加入房间时调用。如果房间服务器不存在则创建。
    /// </summary>
    public (RoomEntityServer server, int port) EnsureRoomServer(string roomId) {
        if (!_roomServers.TryGetValue(roomId, out var server))
            server = CreateRoomServer(roomId);
        return (server, server.Port);
    }

    #region Interactive Console

    /// <summary>
    /// 阻塞式交互 CLI 循环。回车后回到调用方。
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
                        CreateRoomServer(parts[1]);
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

    #endregion
}