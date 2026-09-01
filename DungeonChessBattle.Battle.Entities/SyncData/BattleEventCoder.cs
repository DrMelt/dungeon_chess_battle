using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;

namespace DungeonChessBattle.Battle.Entities.SyncData;

/// <summary>
/// 领域战斗事件与 SyncBattleEvent 的双向映射，事件类型 tag 与槽位语义唯一权威来源。
/// 服务端编码整帧事件日志，客户端解码回领域事件；新增领域事件类型只需补充本类 tag 与映射。
/// 解码遇未知 tag 返回 null，由调用方跳过，网络协议向前兼容。
/// tag 只增不改不复用：7 随单位死亡事件一并退役，死亡由生命值派生。
/// </summary>
public static class BattleEventCoder {
    /// <summary>单位受到伤害。</summary>
    public const byte TypeDamage = 1;

    /// <summary>单位接受治疗。</summary>
    public const byte TypeHeal = 2;

    /// <summary>仇恨修改请求。</summary>
    public const byte TypeHateRequested = 3;

    /// <summary>单位获得 Buff。</summary>
    public const byte TypeBuffApplied = 4;

    /// <summary>单位失去 Buff。</summary>
    public const byte TypeBuffExpired = 5;

    /// <summary>技能读条完成并结算。</summary>
    public const byte TypeCastCompleted = 6;

    /// <summary>技能开始读条施法。</summary>
    public const byte TypeCastStarted = 8;

    /// <summary>施法读条被主动取消，含移动打断。</summary>
    public const byte TypeCastCanceled = 9;

    /// <summary>编码单个领域事件。未知事件类型抛异常，配置故障响亮暴露。</summary>
    public static SyncBattleEvent Encode(IBattleEvent evt) {
        return evt switch {
            DamageOccurred d => new SyncBattleEvent {
                Type = TypeDamage, A = d.SourceUnitId, B = d.TargetUnitId,
                C = (byte)d.DamageType, Value = d.AppliedDamage,
            },
            HealOccurred h => new SyncBattleEvent {
                Type = TypeHeal, A = h.SourceUnitId, B = h.TargetUnitId, Value = h.ActualHeal,
            },
            HateRequested hr => new SyncBattleEvent {
                Type = TypeHateRequested, A = hr.HolderUnitId, B = hr.SourceUnitId,
                C = (byte)hr.Op, Value = hr.Value,
            },
            BuffApplied ba => new SyncBattleEvent {
                Type = TypeBuffApplied, A = ba.TargetUnitId, B = ba.BuffTypeId, C = (ushort)ba.StackCount,
            },
            BuffExpired be => new SyncBattleEvent { Type = TypeBuffExpired, A = be.TargetUnitId, B = be.BuffTypeId },
            CastCompleted cc => new SyncBattleEvent {
                Type = TypeCastCompleted, A = cc.CasterUnitId, C = cc.TargetUnitId?.Value ?? 0, SkillKey = cc.SkillId.Id,
            },
            CastStarted cs => new SyncBattleEvent {
                Type = TypeCastStarted, A = cs.CasterUnitId, C = cs.TargetUnitId?.Value ?? 0, SkillKey = cs.SkillId.Id,
            },
            CastCanceled ccl => new SyncBattleEvent {
                Type = TypeCastCanceled, A = ccl.CasterUnitId, SkillKey = ccl.SkillId.Id,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(evt), evt.GetType(), "Unknown battle event type."),
        };
    }

    /// <summary>解码单个同步事件为领域事件；未知 tag 或技能键非法返回 null，由调用方按丢弃处理。</summary>
    public static IBattleEvent? Decode(SyncBattleEvent e) {
        if ((e.Type is TypeCastStarted or TypeCastCompleted or TypeCastCanceled)
            && (string.IsNullOrEmpty(e.SkillKey) || e.SkillKey.Length > SkillKeyId.MaxKeyLength))
            return null;
        return e.Type switch {
            TypeDamage => new DamageOccurred(e.A, e.B, e.Value, (DamageType)e.C),
            TypeHeal => new HealOccurred(e.A, e.B, e.Value),
            TypeHateRequested => new HateRequested(e.A, e.B, (HateEffectOp)e.C, e.Value),
            TypeBuffApplied => new BuffApplied(e.A, e.B, e.C),
            TypeBuffExpired => new BuffExpired(e.A, e.B),
            TypeCastCompleted => new CastCompleted(e.A, new SkillKeyId(e.SkillKey), e.C == 0 ? null : new UnitId(e.C)),
            TypeCastStarted => new CastStarted(e.A, new SkillKeyId(e.SkillKey), e.C == 0 ? null : new UnitId(e.C)),
            TypeCastCanceled => new CastCanceled(e.A, new SkillKeyId(e.SkillKey)),
            _ => null,
        };
    }
}
