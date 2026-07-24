using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;
using DungeonChessBattle.Logic.Rooms;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// Logic 层对外门面服务，实现 IBattleService。
/// 组合 RoomManager 和 BattleResolver，提供房间管理、战斗流程、技能结算等全部业务操作。
/// </summary>
public class GameLogicService : IBattleService {
    private readonly RoomManager _roomManager = new();

    #region Room Management

    public GameRoom CreateRoom(string roomId) => _roomManager.CreateRoom(roomId);

    public GameRoom? GetRoom(string roomId) => _roomManager.GetRoom(roomId);

    public bool RemoveRoom(string roomId) => _roomManager.RemoveRoom(roomId);

    public IEnumerable<GameRoom> GetAllRooms() => _roomManager.GetAllRooms();

    #endregion

    #region Battle Flow

    public BattleManager StartBattleInRoom(string roomId) {
        _ = GetRoom(roomId)
            ?? throw new InvalidOperationException($"Room {roomId} not found.");

        var battle = _roomManager.GetOrCreateBattle(roomId);
        battle.StartBattle();
        return battle;
    }

    public BattleManager? GetBattle(string roomId) => _roomManager.GetBattle(roomId);

    public void AdvancePhase(BattleManager battle) => battle.AdvancePhase();

    public void NextRound(BattleManager battle) => battle.NextRound();

    public void EndBattle(BattleManager battle) => battle.EndBattle();

    #endregion

    #region Skill

    public void CastSkill(BattleManager battle, UnitModel caster, UnitModel target, SkillModel skill) {
        BattleResolver.ApplySkillDamage(caster, target, skill);
        BattleResolver.ApplySkillCure(caster, target, skill);
    }

    #endregion

    #region Buffs & Status

    public void UpdateBuffs(BattleManager battle, IEnumerable<UnitModel> units, double deltaTime) {
        foreach (var unit in units) {
            BattleResolver.UpdateUnitBuffs(unit, deltaTime);
        }
    }

    public bool CheckBattleEnded(GameRoom room) {
        bool aAlive = BattleResolver.HasAliveUnits(room.UnitsA);
        bool bAlive = BattleResolver.HasAliveUnits(room.UnitsB);
        return !aAlive || !bAlive;
    }

    #endregion
}