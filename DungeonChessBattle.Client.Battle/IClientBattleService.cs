using DungeonChessBattle.Protocol.Enums;
using BattlePhase = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;
using BuffView = DungeonChessBattle.Battle.Domain.Combat.BuffView;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 客户端战斗服务接口，仅包含客户端需要的查询与操作。
/// 所有方法使用 roomId 作为上下文标识，不暴露服务端实现细节。
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
    event Action<string, BuffView>? UnitBuffAdded;

    /// <summary>单位移除 Buff 事件。参数：单位名称、Buff 数据</summary>
    event Action<string, BuffView>? UnitBuffRemoved;

    /// <summary>
    /// 获取房间的服务端权威创建时间（UTC Unix 秒）。
    /// 未进入房间或实体同步未完成时返回 null。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    long? GetRoomCreatedUnixTime(string roomId);

    /// <summary>
    /// 在指定房间创建单位。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitName">单位名称。</param>
    /// <param name="camp">阵营字符串标识（如 "Camp_A"、"Camp_B"）。</param>
    void CreateUnit(string roomId, string unitName, string camp);

    /// <summary>
    /// 对目标施放技能（客户端发起）。通过 RPC 发送，服务端权威读条与结算。
    /// 参数展开为值类型，避免接口层依赖轻量实体类型。
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
