using DungeonChessBattle.Core.Interfaces;
using DungeonChessBattle.Core.Models;
using DungeonChessBattle.Logic.Battle;

namespace DungeonChessBattle.Logic.Services;

/// <summary>
/// 服务端战斗服务接口，包含房间管理、战斗流程控制等完整操作。
/// 战斗流程面向 <see cref="IBattle"/> 抽象接口，不暴露内部实现类型；
/// 技能结算与 Buff 推进以 roomId/单位集合为上下文，不携带战斗管理器参数。
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
    /// <returns>对应的战斗流程；房间不存在时抛出异常。</returns>
    IBattle StartBattleInRoom(string roomId);

    /// <summary>
    /// 按房间 ID 获取战斗流程。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的战斗流程；不存在时返回 null。</returns>
    IBattle? GetBattle(string roomId);

    /// <summary>
    /// 按帧推进指定房间的战斗逻辑。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    void TickBattle(string roomId, float deltaTime);

    /// <summary>
    /// 结束指定战斗。
    /// </summary>
    /// <param name="battle">要结束的战斗流程。</param>
    void EndBattle(IBattle battle);

    /// <summary>
    /// 对目标施放技能（服务端权威结算）。
    /// </summary>
    /// <param name="caster">施法单位。</param>
    /// <param name="target">目标单位。</param>
    /// <param name="skill">技能模型。</param>
    /// <param name="allUnits">所有可命中的检测单位（范围伤害技能需要）。</param>
    void CastSkill(IUnitState caster, IUnitState target, SkillModel skill,
        IReadOnlyList<IUnitState>? allUnits = null);

    /// <summary>
    /// 按帧推进单位集合的 Buff 状态（服务端权威结算后下推）。
    /// </summary>
    /// <param name="units">要更新的单位集合。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    void UpdateBuffs(IEnumerable<IUnitState> units, double deltaTime);

    /// <summary>
    /// 判断战斗是否已结束。
    /// </summary>
    /// <param name="room">房间数据。</param>
    /// <returns>已结束返回 true。</returns>
    bool CheckBattleEnded(GameRoom room);
}
