using DungeonChessBattle.Battle.Shared.Combat;

namespace DungeonChessBattle.Battle.Shared.Events;

/// <summary>
/// 战斗事件的统一标记接口。事件为纯数据，由编排层经可靠通道外送 / SyncVar 写回。
/// 单位死亡不是事件：它是生命值派生的状态，一律经 IsDead 判定，避免状态与一次性事件两份真相。
/// </summary>
public interface IBattleEvent {
}

/// <summary>单位受到伤害。SourceNetId 为伤害来源，UnitId.None 表示无来源不记仇。</summary>
public readonly record struct DamageOccurred(UnitId SourceNetId, UnitId TargetNetId, float AppliedDamage, DamageType DamageType) : IBattleEvent;

/// <summary>单位接受治疗。SourceNetId 为治疗来源，UnitId.None 表示无来源不记仇。</summary>
public readonly record struct HealOccurred(UnitId SourceNetId, UnitId TargetNetId, float ActualHeal) : IBattleEvent;

/// <summary>仇恨修改请求：由仇恨技能结算产出，目标单位按自身仇恨规则决定是否响应。</summary>
public readonly record struct HateRequested(UnitId HolderNetId, UnitId SourceNetId, HateEffectOp Op, float Value) : IBattleEvent;

/// <summary>单位获得 Buff。</summary>
public readonly record struct BuffApplied(UnitId TargetNetId, ushort BuffTypeId, int StackCount) : IBattleEvent;

/// <summary>单位失去 Buff。</summary>
public readonly record struct BuffExpired(UnitId TargetNetId, ushort BuffTypeId) : IBattleEvent;

/// <summary>技能读条完成并完成结算。</summary>
public readonly record struct CastCompleted(UnitId CasterNetId, SkillKeyId SkillId, UnitId? TargetNetId) : IBattleEvent;

/// <summary>技能开始读条施法。</summary>
public readonly record struct CastStarted(UnitId CasterNetId, SkillKeyId SkillId, UnitId? TargetNetId) : IBattleEvent;

/// <summary>施法读条被主动取消，含移动打断。</summary>
public readonly record struct CastCanceled(UnitId CasterNetId, SkillKeyId SkillId) : IBattleEvent;
