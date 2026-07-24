using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Rooms;

namespace DungeonChessBattle.Logic.Services;

public class GameLogicService
{
    private readonly RoomManager _roomManager = new();
    private readonly BattleResolver _battleResolver = new();

    public GameRoom CreateRoom(string roomId)
    {
        var room = _roomManager.CreateRoom(roomId);
        return room;
    }

    public GameRoom? GetRoom(string roomId) => _roomManager.GetRoom(roomId);

    public bool RemoveRoom(string roomId) => _roomManager.RemoveRoom(roomId);

    public BattleManager StartBattleInRoom(string roomId)
    {
        var room = _roomManager.GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battleManager = new BattleManager();
        battleManager.StartBattle();
        return battleManager;
    }

    public void CastSkill(BattleManager battle, UnitModel caster, UnitModel target, SkillModel skill)
    {
        _battleResolver.ApplySkillDamage(caster, target, skill);
        _battleResolver.ApplySkillCure(caster, target, skill);
    }

    public void UpdateBuffs(BattleManager battle, IEnumerable<UnitModel> units, double deltaTime)
    {
        foreach (var unit in units)
        {
            _battleResolver.UpdateUnitBuffs(unit, deltaTime);
        }
    }

    public bool CheckBattleEnded(GameRoom room)
    {
        bool aAlive = _battleResolver.HasAliveUnits(room.UnitsA);
        bool bAlive = _battleResolver.HasAliveUnits(room.UnitsB);
        return !aAlive || !bAlive;
    }
}