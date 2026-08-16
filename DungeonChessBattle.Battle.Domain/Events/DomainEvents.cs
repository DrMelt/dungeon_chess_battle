using DungeonChessBattle.Battle.Domain.Combat;

namespace DungeonChessBattle.Battle.Domain.Events;

/// <summary>领域事件的统一标记接口。事件为纯数据，由编排层转译成网络 RPC / SyncVar 写回。</summary>
public interface IDomainEvent {
}

/// <summary>单位受到伤害。SourceNetId 为伤害来源，0 表示无来源不记仇。</summary>
public readonly record struct DamageOccurred(ushort SourceNetId, ushort TargetNetId, float AppliedDamage, DamageType DamageType) : IDomainEvent;

/// <summary>单位接受治疗。SourceNetId 为治疗来源，0 表示无来源不记仇。</summary>
public readonly record struct HealOccurred(ushort SourceNetId, ushort TargetNetId, float ActualHeal) : IDomainEvent;

/// <summary>仇恨修改请求：由仇恨技能结算产出，目标单位按自身仇恨规则决定是否响应。</summary>
public readonly record struct HateRequested(ushort HolderNetId, ushort SourceNetId, HateEffectOp Op, float Value) : IDomainEvent;

/// <summary>单位获得 Buff。</summary>
public readonly record struct BuffApplied(ushort TargetNetId, ushort BuffTypeId, int StackCount) : IDomainEvent;

/// <summary>单位失去 Buff。</summary>
public readonly record struct BuffExpired(ushort TargetNetId, ushort BuffTypeId) : IDomainEvent;

/// <summary>技能读条完成并完成结算。</summary>
public readonly record struct CastCompleted(ushort CasterNetId, SkillKeyId SkillId, ushort? TargetNetId) : IDomainEvent;

/// <summary>单位死亡。</summary>
public readonly record struct UnitDied(ushort UnitNetId) : IDomainEvent;

/// <summary>战斗开始，阶段机进入 Running，房间级元事件。</summary>
public readonly record struct BattleStarted() : IDomainEvent;

/// <summary>战斗结束，阶段机进入 Finished。WinnerCamp 为胜方阵营；多个阵营存活判定为未结束，null 表示平局或无存活。</summary>
public readonly record struct BattleEnded(string? WinnerCamp) : IDomainEvent;
