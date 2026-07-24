using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Rooms;

public class GameRoom
{
    public string RoomId { get; }
    public List<UnitModel> UnitsA { get; } = [];
    public List<UnitModel> UnitsB { get; } = [];
    public bool IsActive { get; set; }

    public GameRoom(string roomId)
    {
        RoomId = roomId;
        IsActive = true;
    }
}

public class RoomManager
{
    private readonly Dictionary<string, GameRoom> _rooms = [];

    public GameRoom CreateRoom(string roomId)
    {
        var room = new GameRoom(roomId);
        _rooms[roomId] = room;
        return room;
    }

    public GameRoom? GetRoom(string roomId)
    {
        _rooms.TryGetValue(roomId, out var room);
        return room;
    }

    public bool RemoveRoom(string roomId)
    {
        var room = GetRoom(roomId);
        if (room == null)
            return false;

        room.IsActive = false;
        return _rooms.Remove(roomId);
    }

    public IEnumerable<GameRoom> GetAllRooms() => _rooms.Values;
}