using System.Collections.Generic;
using BattlePhase = DungeonChessBattle.Battle.Domain.Combat.BattlePhase;
using DungeonChessBattle.Battle.Domain.Events;

namespace DungeonChessBattle.Client.Battle;

/// <summary>
/// 客户端战斗服务接口，仅包含客户端需要的查询与操作。
/// 所有方法使用 roomId 作为上下文标识，不暴露服务端实现细节。
/// </summary>
public interface IClientBattleService {
    /// <summary>单位创建事件。参数：房间 ID、单位网络实体 ID、单位名称、阵营列表。</summary>
    event Action<string, ushort, string, IReadOnlyList<string>>? OnUnitCreated;

    /// <summary>战斗阶段变化事件。参数：房间 ID、战斗阶段。</summary>
    event Action<string, BattlePhase>? BattlePhaseChanged;

    /// <summary>战斗事件日志事件。参数：房间 ID、本帧领域事件列表。</summary>
    event Action<string, IReadOnlyList<IBattleEvent>>? BattleEventsReceived;

    /// <summary>单位生命值变化事件。参数：单位网络实体 ID、新生命值、旧生命值。</summary>
    event Action<ushort, float, float>? UnitHealthChanged;

    /// <summary>单位死亡事件。参数：单位网络实体 ID。</summary>
    event Action<ushort>? UnitDied;

    /// <summary>单位聚焦目标变化事件。参数：单位网络实体 ID、目标单位网络实体 ID，0 表示无聚焦目标。</summary>
    event Action<ushort, ushort>? UnitFocusTargetChanged;

    /// <summary>
    /// 对目标施放技能，客户端发起。经可靠请求通道发送，服务端权威读条与结算。
    /// 参数展开为值类型，避免接口层依赖轻量实体类型。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="casterNetId">施法单位网络实体 ID。</param>
    /// <param name="targetNetId">目标单位网络实体 ID，范围伤害技能传 0。</param>
    /// <param name="skillId">技能配置 ID。</param>
    /// <param name="targetPosX">位置目标 X，范围伤害技能使用。</param>
    /// <param name="targetPosZ">位置目标 Z，范围伤害技能使用。</param>
    void CastSkill(string roomId, ushort casterNetId, ushort targetNetId, ushort skillId,
        float targetPosX = 0f, float targetPosZ = 0f);

    /// <summary>
    /// 设置单位聚焦目标，客户端发起。经可靠请求通道发送，服务端校验后写回权威状态。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <param name="unitNetId">设置聚焦目标的单位网络实体 ID。</param>
    /// <param name="targetNetId">目标单位网络实体 ID，传 0 表示清除聚焦目标。</param>
    void SetFocusTarget(string roomId, ushort unitNetId, ushort targetNetId);

    /// <summary>
    /// 判断指定房间的战斗是否已结束。
    /// </summary>
    /// <param name="roomId">房间 ID。</param>
    /// <returns>已结束返回 true。</returns>
    bool CheckBattleEnded(string roomId);

    /// <summary>提交玩家移动输入。参数展开为 float 避免接口层依赖 System.Numerics。
    /// 输入流仅承载移动状态；技能等一次性事件走 CastSkill / SetFocusTarget 请求。</summary>
    void SubmitPlayerInput(float moveX, float moveY);

    /// <summary>
    /// 当前房间会话的事件日志。返回内部列表只读视图，仅可枚举；
    /// 断线/重连/离开房间时清空，UI 据索引做增量同步与历史回填。
    /// </summary>
    IReadOnlyList<BattleEventLogEntry> GetEventLog();

    /// <summary>
    /// 当前房间会话事件日志的版本号，Clear 会话重置时自增。
    /// 与 GetEventLog 配对消费：版本变化即会话切换，UI 游标归零重同步。
    /// </summary>
    long GetEventLogVersion();
}
