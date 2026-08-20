using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain.Events;

/// <summary>战斗事件的统一标记接口。事件为纯数据，由编排层转译成网络 RPC / SyncVar 写回。</summary>
public interface IBattleEvent {
}

/// <summary>单位受到伤害。SourceNetId 为伤害来源，0 表示无来源不记仇。</summary>
public readonly record struct DamageOccurred(ushort SourceNetId, ushort TargetNetId, float AppliedDamage, DamageType DamageType) : IBattleEvent;

/// <summary>单位接受治疗。SourceNetId 为治疗来源，0 表示无来源不记仇。</summary>
public readonly record struct HealOccurred(ushort SourceNetId, ushort TargetNetId, float ActualHeal) : IBattleEvent;

/// <summary>仇恨修改请求：由仇恨技能结算产出，目标单位按自身仇恨规则决定是否响应。</summary>
public readonly record struct HateRequested(ushort HolderNetId, ushort SourceNetId, HateEffectOp Op, float Value) : IBattleEvent;

/// <summary>单位获得 Buff。</summary>
public readonly record struct BuffApplied(ushort TargetNetId, ushort BuffTypeId, int StackCount) : IBattleEvent;

/// <summary>单位失去 Buff。</summary>
public readonly record struct BuffExpired(ushort TargetNetId, ushort BuffTypeId) : IBattleEvent;

/// <summary>技能读条完成并完成结算。</summary>
public readonly record struct CastCompleted(ushort CasterNetId, SkillKeyId SkillId, ushort? TargetNetId) : IBattleEvent;

/// <summary>单位死亡。</summary>
public readonly record struct UnitDied(ushort UnitNetId) : IBattleEvent;
