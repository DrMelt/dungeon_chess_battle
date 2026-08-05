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
    /// <summary>
    /// 创建房间。
    /// </summary>
    /// <param name="roomId">房间唯一 ID。</param>
    /// <returns>新建的房间。</returns>
    GameRoom CreateRoom(string roomId);

    /// <summary>
    /// 按 ID 获取房间。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的房间；不存在时返回 null。</returns>
    GameRoom? GetRoom(string roomId);

    /// <summary>
    /// 移除房间。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>移除成功返回 true；房间不存在返回 false。</returns>
    bool RemoveRoom(string roomId);

    /// <summary>获取全部房间。</summary>
    IEnumerable<GameRoom> GetAllRooms();

    /// <summary>
    /// 在指定房间开始战斗。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>房间对应的战斗管理器。</returns>
    BattleManager StartBattleInRoom(string roomId);

    /// <summary>
    /// 按房间 ID 获取战斗实例。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的战斗管理器；不存在时返回 null。</returns>
    BattleManager? GetBattle(string roomId);

    /// <summary>
    /// 按帧推进战斗逻辑。
    /// </summary>
    /// <param name="battle">战斗管理器。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    void TickBattle(BattleManager battle, float deltaTime);

    /// <summary>
    /// 结束战斗。
    /// </summary>
    /// <param name="battle">战斗管理器。</param>
    void EndBattle(BattleManager battle);

    /// <summary>
    /// 对目标施放技能（服务端权威结算）。
    /// </summary>
    /// <param name="battle">战斗管理器。</param>
    /// <param name="caster">施法单位。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">技能模型。</param>
    /// <param name="allUnits">所有可命中的检测单位（范围伤害技能需要）。</param>
    void CastSkill(BattleManager battle, IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null);

    /// <summary>
    /// 按帧推进单位集合的 Buff 状态。
    /// </summary>
    /// <param name="battle">战斗管理器。</param>
    /// <param name="units">要更新的单位集合。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    void UpdateBuffs(BattleManager battle, IEnumerable<IUnitState> units, double deltaTime);

    /// <summary>
    /// 判断战斗是否已结束。
    /// </summary>
    /// <param name="room">房间数据。</param>
    /// <returns>已结束返回 true。</returns>
    bool CheckBattleEnded(GameRoom room);
}
