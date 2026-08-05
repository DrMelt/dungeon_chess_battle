using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;

namespace DungeonChessBattle.Logic.Rooms;

/// <summary>
/// 房间管理器：维护房间与对应战斗实例的创建、查询与销毁。
/// </summary>
public class RoomManager {
    private readonly Dictionary<string, GameRoom> _rooms = [];
    private readonly Dictionary<string, BattleManager> _battles = [];

    /// <summary>
    /// 创建并登记一个房间。
    /// </summary>
    /// <param name="roomId">房间唯一 ID。</param>
    /// <returns>新建的房间。</returns>
    public GameRoom CreateRoom(string roomId) {
        var room = new GameRoom(roomId);
        _rooms[roomId] = room;
        return room;
    }

    /// <summary>
    /// 按 ID 获取房间。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的房间；不存在时返回 null。</returns>
    public GameRoom? GetRoom(string roomId) {
        _rooms.TryGetValue(roomId, out var room);
        return room;
    }

    /// <summary>
    /// 移除房间及其战斗实例。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>移除成功返回 true；房间不存在返回 false。</returns>
    public bool RemoveRoom(string roomId) {
        var room = GetRoom(roomId);
        if (room == null)
            return false;

        room.IsActive = false;
        _battles.Remove(roomId);
        return _rooms.Remove(roomId);
    }

    /// <summary>获取全部房间。</summary>
    public IEnumerable<GameRoom> GetAllRooms() => _rooms.Values;

    /// <summary>
    /// 获取房间对应的战斗实例，不存在时创建。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的战斗管理器。</returns>
    public BattleManager GetOrCreateBattle(string roomId) {
        if (_battles.TryGetValue(roomId, out var existing))
            return existing;

        var battle = new BattleManager();
        _battles[roomId] = battle;
        return battle;
    }

    /// <summary>
    /// 按房间 ID 获取战斗实例。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的战斗管理器；不存在时返回 null。</returns>
    public BattleManager? GetBattle(string roomId) {
        _battles.TryGetValue(roomId, out var battle);
        return battle;
    }
}
