using System.Numerics;
using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Battle.Domain.Movement;

namespace DungeonChessBattle.Battle.Domain;

/// <summary>
/// 战斗世界契约：继承 <see cref="IBattleSceneView"/> 作为 AI 决策查询入口，
/// 追加写成员与逐帧推进方法作为编排层唯一更新入口。
/// 写成员与推进方法为服务端权威，由编排层调用；查询成员供 AI 决策只读使用。
/// 移动结算不在本契约内：位置由实体确定性结算（UnitPawn.Update）。
/// 世界只拥有单位注册表；房间级阶段状态经 <see cref="IBattleRoom"/> 读写载体，
/// 读条目标、Buff、冷却、仇恨等单位权威状态由单位经 <see cref="IBattleUnit.RuntimeState"/> 承载，场景只做推进与投影。
/// </summary>
public interface IBattleScene : IBattleSceneView {
    /// <summary>竞技场移动场景，供实体层接线移动结算；战斗结算不消费位移。构造后只读。</summary>
    IMovementScene MovementScene {
        get;
    }

    /// <summary>注册一个战斗单位。</summary>
    void AddUnit(IBattleUnit unit);

    /// <summary>移除已注册的战斗单位。</summary>
    void RemoveUnit(IBattleUnit unit);

    /// <summary>开始战斗：Waiting 到 Running，阶段状态经 <see cref="IBattleRoom"/> 写入载体。</summary>
    void StartBattle();

    /// <summary>手动结束战斗，幂等兜底。</summary>
    void EndBattle();

    /// <summary>发起读条施法，技能存在、归属、状态与目标/位置校验通过返回 true。</summary>
    bool BeginCast(IBattleUnit caster, SkillKeyId skillKey, IBattleUnit? target, Vector2? targetPos);

    /// <summary>单位发生移动，保留既定行为"移动即打断读条"。</summary>
    void OnUnitMoved(IBattleUnit unit, Vector2 moveDir);

    /// <summary>取消单位当前读条施法，产生 CastCanceled 事件并清理读条状态；无读条为空操作。</summary>
    void CancelCast(IBattleUnit unit);

    /// <summary>
    /// AI 前置推进：逐个触发单位的自治决策，动作执行经单位绑定执行器回到场景。
    /// 必须在实体移动结算（UnitPawn.Update）之前调用，移动输入本帧生效。
    /// </summary>
    void ApplyDecisions();

    /// <summary>战斗推进：读条、冷却、Buff、仇恨与死亡结算，返回本帧领域事件。仅在 Running 阶段推进。</summary>
    IReadOnlyList<IBattleEvent> Tick(double deltaTime);
}

