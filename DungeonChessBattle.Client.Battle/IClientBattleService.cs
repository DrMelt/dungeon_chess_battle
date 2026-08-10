using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Models;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 客户端战斗服务接口，仅包含客户端需要的查询与操作。
/// 所有方法使用 roomId 作为上下文标识，不暴露 BattleManager。
/// </summary>
public interface IClientBattleService {
    /// <summary>单位创建事件。参数：房间ID、单位名称、阵营(字符串)</summary>
    event Action<string, string, string>? OnUnitCreated;

    /// <summary>战斗阶段变化事件。参数：房间ID、战斗阶段</summary>
    event Action<string, BattlePhase>? BattlePhaseChanged;

    /// <summary>单位生命值变化事件。参数：单位名称、新生命值、旧生命值</summary>
    event Action<string, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位名称</summary>
    event Action<string>? UnitDied;

    /// <summary>单位添加 Buff 事件。参数：单位名称、Buff 数据</summary>
    event Action<string, BuffEventData>? UnitBuffAdded;

    /// <summary>单位移除 Buff 事件。参数：单位名称、Buff 数据</summary>
    event Action<string, BuffEventData>? UnitBuffRemoved;

    /// <summary>
    /// 按 ID 获取房间。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>对应的房间；不存在时返回 null。</returns>
    GameRoom? GetRoom(string roomId);

    /// <summary>获取全部房间。</summary>
    IEnumerable<GameRoom> GetAllRooms();

    /// <summary>
    /// 创建房间。
    /// </summary>
    /// <param name="roomId">房间唯一 ID。</param>
    /// <returns>新建的房间。</returns>
    GameRoom CreateRoom(string roomId);

    /// <summary>
    /// 在指定房间创建单位。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitName">单位名称。</param>
    /// <param name="camp">阵营字符串标识（如 "Camp_A"、"Camp_B"）。</param>
    /// <returns>创建的单位状态；客户端实现返回 null（单位由 Pawn 实体承载）。</returns>
    IUnitState? CreateUnit(string roomId, string unitName, string camp);

    /// <summary>
    /// 对目标施放技能（客户端发起）。通过 RPC 发送，服务端权威读条与结算。
    /// 参数展开为值类型，避免接口层依赖 IUnitState/Entities。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="casterName">施法单位名称。</param>
    /// <param name="targetName">目标单位名称（范围伤害技能传 null）。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <param name="targetPosX">位置目标 X（范围伤害技能使用）。</param>
    /// <param name="targetPosZ">位置目标 Z（范围伤害技能使用）。</param>
    void CastSkill(string roomId, string casterName, string? targetName, ushort skillId,
        float targetPosX = 0f, float targetPosZ = 0f);

    /// <summary>
    /// 按帧推进单位集合的 Buff 状态（服务端权威结算后下推）。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="units">要更新的单位集合。</param>
    /// <param name="deltaTime">距上一帧的间隔时间（秒）。</param>
    void UpdateBuffs(string roomId, IEnumerable<IUnitState> units, double deltaTime);

    /// <summary>
    /// 判断指定房间的战斗是否已结束。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>已结束返回 true。</returns>
    bool CheckBattleEnded(string roomId);

    /// <summary>请求开始战斗。通过 RPC 发送。</summary>
    void RequestStartBattle(string roomId);

    /// <summary>提交玩家输入。参数展开为 float 避免接口层依赖 System.Numerics。</summary>
    void SubmitPlayerInput(float moveX, float moveY, byte skillFlags, float aimX, float aimY);
}
