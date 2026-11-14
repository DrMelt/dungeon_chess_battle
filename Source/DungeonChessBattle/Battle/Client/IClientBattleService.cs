namespace DungeonChessBattle.Battle.Client;

/// <summary>
/// 客户端战斗服务接口，仅包含客户端需要的查询与操作。
/// 一个连接对应一个房间，方法不接收房间标识；房间归属经事件载荷表达。
/// </summary>
public interface IClientBattleService {
    /// <summary>战斗阶段变化事件。</summary>
    event Action<BattlePhaseChange>? BattlePhaseChanged;

    /// <summary>战斗事件日志事件。</summary>
    event Action<BattleEventBatch>? BattleEventsReceived;

    /// <summary>
    /// 本地内容与服务端不一致事件。
    /// 检测到即放弃本地战斗世界，由装配层退出战斗并提示玩家，本端不做降级。
    /// </summary>
    event Action<BattleContentMismatch>? ContentMismatchDetected;

    /// <summary>
    /// 对目标施放技能，客户端发起。经可靠请求通道发送，服务端权威读条与结算。
    /// 施法者不由本方法指定：服务端从请求来源控制器持有的单位推导，杜绝伪造施法者。
    /// 参数展开为值类型，避免接口层依赖轻量实体类型。
    /// </summary>
    /// <param name="targetNetId">目标单位网络实体 ID，范围伤害技能传 0。</param>
    /// <param name="skillId">技能配置键。</param>
    /// <param name="targetPosX">位置目标 X，范围伤害技能使用。</param>
    /// <param name="targetPosZ">位置目标 Z，范围伤害技能使用。</param>
    void CastSkill(ushort targetNetId, string skillId,
        float targetPosX = 0f, float targetPosZ = 0f);

    /// <summary>
    /// 设置本地玩家单位的聚焦目标，客户端发起。经可靠请求通道发送，服务端校验后写回权威状态。
    /// 聚焦持有者不由本方法指定：服务端从请求来源控制器持有的单位推导。
    /// </summary>
    /// <param name="targetNetId">目标单位网络实体 ID，传 0 表示清除聚焦目标。</param>
    void SetFocusTarget(ushort targetNetId);

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
