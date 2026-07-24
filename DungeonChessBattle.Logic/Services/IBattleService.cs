using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// 战斗服务抽象接口，定义 Logic 层对外暴露的全部操作。
/// 本地实现直接调用 Logic，网络实现通过 RPC 转发到服务端。
/// </summary>
public interface IBattleService {
    // 房间管理
    GameRoom CreateRoom(string roomId);
    GameRoom? GetRoom(string roomId);
    bool RemoveRoom(string roomId);
    IEnumerable<GameRoom> GetAllRooms();

    // 战斗流程
    BattleManager StartBattleInRoom(string roomId);
    BattleManager? GetBattle(string roomId);
    void AdvancePhase(BattleManager battle);
    void NextRound(BattleManager battle);
    void EndBattle(BattleManager battle);

    // 技能
    void CastSkill(BattleManager battle, UnitModel caster, UnitModel target, SkillModel skill);

    // Buff 更新
    void UpdateBuffs(BattleManager battle, IEnumerable<UnitModel> units, double deltaTime);

    // 胜负判定
    bool CheckBattleEnded(GameRoom room);
}