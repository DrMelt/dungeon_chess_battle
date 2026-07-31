using System.Numerics;
using DungeonChessBattle.Entities;
using DungeonChessBattle.Logic.Services;
using DungeonChessBattle.Server.Network;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅模块，负责房间/单位的 CRUD、实体缓存和交互式 CLI。
/// 实时化：完全使用 UnitPawn，不再依赖 UnitSyncEntity。
/// </summary>
public class GameLobby(EntityNetworkServer networkServer, IServerBattleService battleService, ILogger<GameLobby> logger) {
    private readonly EntityNetworkServer _networkServer = networkServer;
    private readonly IServerBattleService _battleService = battleService;
    private readonly ILogger<GameLobby> _logger = logger;

    private readonly Dictionary<string, BattleRoomEntity> _roomEntities = [];
    private readonly Dictionary<string, List<UnitPawn>> _roomPawns = [];

    public IReadOnlyDictionary<string, BattleRoomEntity> Rooms => _roomEntities;
    public IReadOnlyDictionary<string, List<UnitPawn>> RoomPawns => _roomPawns;

    public BattleRoomEntity CreateRoomEntity(string roomId) {
        if (_roomEntities.TryGetValue(roomId, out BattleRoomEntity? value))
            return value;
        var entity = _networkServer.EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = roomId;
        }) ?? throw new InvalidOperationException($"Failed to create BattleRoomEntity for room '{roomId}'.");
        _roomEntities[roomId] = entity;
        _roomPawns[roomId] = [];

        // 同步在 Logic 层创建对应房间
        _battleService.CreateRoom(roomId);

        return entity;
    }

    /// <summary>
    /// 创建实时 UnitPawn 实体。
    /// </summary>
    public UnitPawn CreatePawnEntity(string roomId, string unitName, byte camp, Vector2 spawnPos) {
        var entity = _networkServer.EntityManager.AddEntity<UnitPawn>(e => {
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
        _battleService.RemoveRoom(roomId);
        return true;
    }

    public void ListRooms() {
        foreach (var (id, room) in _roomEntities) {
            if (_logger.IsEnabled(LogLevel.Information))
                _logger.LogInformation("  {RoomId}: Phase={Phase}, Pawns={PawnCount}", id, room.BattlePhase.Value, _roomPawns.GetValueOrDefault(id)?.Count ?? 0);
        }
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
                        CreateRoomEntity(parts[1]);
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
