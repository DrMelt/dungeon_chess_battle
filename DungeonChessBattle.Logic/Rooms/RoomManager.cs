using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;

namespace DungeonChessBattle.Logic.Rooms;

public class RoomManager {
    private readonly Dictionary<string, GameRoom> _rooms = [];
    private readonly Dictionary<string, BattleManager> _battles = [];

    public GameRoom CreateRoom(string roomId) {
        var room = new GameRoom(roomId);
        _rooms[roomId] = room;
        return room;
    }

    public GameRoom? GetRoom(string roomId) {
        _rooms.TryGetValue(roomId, out var room);
        return room;
    }

    public bool RemoveRoom(string roomId) {
        var room = GetRoom(roomId);
        if (room == null)
            return false;

        room.IsActive = false;
        _battles.Remove(roomId);
        return _rooms.Remove(roomId);
    }

    public IEnumerable<GameRoom> GetAllRooms() => _rooms.Values;

    public BattleManager GetOrCreateBattle(string roomId) {
        if (_battles.TryGetValue(roomId, out var existing))
            return existing;

        var battle = new BattleManager();
        _battles[roomId] = battle;
        return battle;
    }

    public BattleManager? GetBattle(string roomId) {
        _battles.TryGetValue(roomId, out var battle);
        return battle;
    }
}
