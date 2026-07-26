using DungeonChessBattle.Entities;
using DungeonChessBattle.Server.Network;

namespace DungeonChessBattle.Server.Lobby;

/// <summary>
/// 大厅模块，负责房间/单位的 CRUD、实体缓存和交互式 CLI。
/// </summary>
public class GameLobby(EntityNetworkServer networkServer) {
    private readonly EntityNetworkServer _networkServer = networkServer;

    private readonly Dictionary<string, BattleRoomEntity> _roomEntities = [];
    private readonly Dictionary<string, List<UnitSyncEntity>> _roomUnits = [];
    private readonly Dictionary<ushort, UnitSyncEntity> _unitById = [];

    public IReadOnlyDictionary<string, BattleRoomEntity> Rooms => _roomEntities;
    public IReadOnlyDictionary<string, List<UnitSyncEntity>> RoomUnits => _roomUnits;

    public BattleRoomEntity CreateRoomEntity(string roomId) {
        if (_roomEntities.TryGetValue(roomId, out BattleRoomEntity? value))
            return value;
        var entity = _networkServer.EntityManager.AddEntity<BattleRoomEntity>(e => {
            e.RoomId.Value = roomId;
        });
        _roomEntities[roomId] = entity!;
        _roomUnits[roomId] = [];
        return entity!;
    }

    public UnitSyncEntity CreateUnitEntity(string roomId, string unitName, byte camp) {
        if (!_roomUnits.TryGetValue(roomId, out var units))
            throw new InvalidOperationException($"Room {roomId} not found.");

        var entity = _networkServer.EntityManager.AddEntity<UnitSyncEntity>(e => {
            e.UnitName.Value = unitName;
            e.Camp.Value = camp;
        });
        units.Add(entity!);
        _unitById[entity!.Id] = entity;
        return entity!;
    }

    public bool RemoveRoom(string roomId) {
        _roomUnits.Remove(roomId);
        return _roomEntities.Remove(roomId);
    }

    public UnitSyncEntity? FindUnitByName(string unitName) {
        foreach (var (_, units) in _roomUnits) {
            var unit = units.Find(u => u.UnitName.Value == unitName);
            if (unit != null)
                return unit;
        }
        return null;
    }

    public void ListRooms() {
        foreach (var (id, room) in _roomEntities) {
            Console.WriteLine($"  {id}: Phase={room.BattlePhase.Value}, Round={room.CurrentRound.Value}, Units={_roomUnits.GetValueOrDefault(id)?.Count ?? 0}");
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
