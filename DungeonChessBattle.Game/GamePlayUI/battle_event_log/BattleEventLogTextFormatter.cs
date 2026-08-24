using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Battle.Domain.Events;
using DungeonChessBattle.Client.Battle;

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
                $"{unitName(d.SourceNetId)} 对 {unitName(d.TargetNetId)} 造成 {d.AppliedDamage:0} 点{DamageTypeText(d.DamageType)}伤害",
            HealOccurred h =>
                $"{unitName(h.SourceNetId)} 治疗 {unitName(h.TargetNetId)} {h.ActualHeal:0} 点生命",
            HateRequested hate =>
                $"{unitName(hate.SourceNetId)} 使 {unitName(hate.HolderNetId)} 仇恨{HateOpText(hate.Op)} {hate.Value:0}",
            BuffApplied buff =>
                $"{unitName(buff.TargetNetId)} 获得 {buffName(buff.BuffTypeId)}",
            BuffExpired buff =>
                $"{unitName(buff.TargetNetId)} 失去 {buffName(buff.BuffTypeId)}",
            CastCompleted cast =>
                cast.TargetNetId is { } target && target != 0
                    ? $"{unitName(cast.CasterNetId)} 对 {unitName(target)} 施放 {skillName(cast.SkillId)}"
                    : $"{unitName(cast.CasterNetId)} 施放 {skillName(cast.SkillId)}",
            CastStarted started =>
                started.TargetNetId is { } target && target != 0
                    ? $"{unitName(started.CasterNetId)} 对 {unitName(target)} 开始施放 {skillName(started.SkillId)}"
                    : $"{unitName(started.CasterNetId)} 开始施放 {skillName(started.SkillId)}",
            CastCanceled canceled =>
                $"{unitName(canceled.CasterNetId)} 取消施放 {skillName(canceled.SkillId)}",
            UnitDied died =>
                $"{unitName(died.UnitNetId)} 死亡",
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
