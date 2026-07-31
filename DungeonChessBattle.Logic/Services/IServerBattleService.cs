using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// 服务端战斗服务接口，包含房间管理、战斗流程控制等完整操作。
/// 服务端内部使用，保留 BattleManager 作为上下文参数。
/// 实时化简化：TickBattle 取代回合制 AdvanceBattlePhase。
/// </summary>
public interface IServerBattleService {
    // 房间管理
    GameRoom CreateRoom(string roomId);
    GameRoom? GetRoom(string roomId);
    bool RemoveRoom(string roomId);
    IEnumerable<GameRoom> GetAllRooms();

    // 战斗流程
    BattleManager StartBattleInRoom(string roomId);
    BattleManager? GetBattle(string roomId);
    void TickBattle(BattleManager battle, float deltaTime);
    void EndBattle(BattleManager battle);

    // 技能
    void CastSkill(BattleManager battle, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null);

    // Buff 更新
    void UpdateBuffs(BattleManager battle, IEnumerable<IUnitState> units, double deltaTime);

    // 胜负判定
    bool CheckBattleEnded(GameRoom room);
}
