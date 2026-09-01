using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Battle.Shared.Events;
using DungeonChessBattle.Battle.Client;

namespace DungeonChessBattle.Game.GamePlayUI.battle_event_log;

/// <summary>
/// 战斗事件日志文字化：把领域事件格式化为可读文本。
/// 名称解析经委托注入，不依赖具体数据源，纯文本逻辑独立可维护。
/// </summary>
public static class BattleEventLogTextFormatter {
    /// <summary>按单位网络 ID 解析显示名。</summary>
    public delegate string UnitNameResolver(ushort netId);

    /// <summary>按技能强类型 ID 解析显示名。</summary>
    public delegate string SkillNameResolver(SkillKeyId skillId);

    /// <summary>按 Buff 类型 ID 解析显示名。</summary>
    public delegate string BuffNameResolver(ushort buffTypeId);

    /// <summary>把日志条目格式化为事件文本，不含时间前缀。</summary>
    public static string Format(BattleEventLogEntry entry,
        UnitNameResolver unitName, SkillNameResolver skillName, BuffNameResolver buffName) {
        return entry.Event switch {
            DamageOccurred d =>
                $"{unitName(d.SourceUnitId)} 对 {unitName(d.TargetUnitId)} 造成 {d.AppliedDamage:0} 点{DamageTypeText(d.DamageType)}伤害",
            HealOccurred h =>
                $"{unitName(h.SourceUnitId)} 治疗 {unitName(h.TargetUnitId)} {h.ActualHeal:0} 点生命",
            HateRequested hate =>
                $"{unitName(hate.SourceUnitId)} 使 {unitName(hate.HolderUnitId)} 仇恨{HateOpText(hate.Op)} {hate.Value:0}",
            BuffApplied buff =>
                $"{unitName(buff.TargetUnitId)} 获得 {buffName(buff.BuffTypeId)}",
            BuffExpired buff =>
                $"{unitName(buff.TargetUnitId)} 失去 {buffName(buff.BuffTypeId)}",
            CastCompleted cast =>
                cast.TargetUnitId is { } target && !target.IsDefault
                    ? $"{unitName(cast.CasterUnitId)} 对 {unitName(target)} 施放 {skillName(cast.SkillId)}"
                    : $"{unitName(cast.CasterUnitId)} 施放 {skillName(cast.SkillId)}",
            CastStarted started =>
                started.TargetUnitId is { } target && !target.IsDefault
                    ? $"{unitName(started.CasterUnitId)} 对 {unitName(target)} 开始施放 {skillName(started.SkillId)}"
                    : $"{unitName(started.CasterUnitId)} 开始施放 {skillName(started.SkillId)}",
            CastCanceled canceled =>
                $"{unitName(canceled.CasterUnitId)} 取消施放 {skillName(canceled.SkillId)}",
            _ => entry.Event.ToString() ?? "",
        };
    }

    private static string DamageTypeText(DamageType damageType) => damageType switch {
        DamageType.Physical => "物理",
        DamageType.Magic => "魔法",
        _ => "",
    };

    private static string HateOpText(HateEffectOp op) => op switch {
        HateEffectOp.Add => "增加",
        HateEffectOp.Multiply => "倍率化",
        HateEffectOp.SetTop => "置顶",
        _ => op.ToString(),
    };
}
