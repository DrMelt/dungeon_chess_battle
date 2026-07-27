using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// 客户端战斗服务接口，仅包含客户端需要的查询与操作。
/// 所有方法使用 roomId 作为上下文标识，不暴露 BattleManager。
/// </summary>
public interface IClientBattleService {
    // 房间查询
    GameRoom? GetRoom(string roomId);
    IEnumerable<GameRoom> GetAllRooms();

    // 技能（客户端发起）
    void CastSkill(string roomId, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null);

    // Buff 更新（服务端权威结算后下推；本地模式由 Logic 层直接处理）
    void UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime);

    // 胜负判定
    bool CheckBattleEnded(string roomId);
}
