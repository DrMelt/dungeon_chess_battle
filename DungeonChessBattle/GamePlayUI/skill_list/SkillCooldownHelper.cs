using DungeonChessBattle.Battle.Domain.Combat;
using DungeonChessBattle.Entities;

namespace DungeonChessBattle.GamePlayUI.skill_list;

/// <summary>
/// 技能冷却查询工具：全局冷却与个体技能冷却取较大者。
/// 按钮冷却显示与施放判定共用同一数据源。
/// </summary>
public static class SkillCooldownHelper {
    /// <summary>计算某技能当前的冷却剩余秒数。</summary>
    /// <param name="pawn">施法单位 Pawn。</param>
    /// <param name="skillKey">技能配置键。</param>
    /// <returns>剩余冷却秒数，无冷却时返回 0。</returns>
    public static float Remaining(UnitPawn pawn, SkillKeyId skillKey) {
        float remaining = pawn.GcdRemaining.Value;
        foreach (var cd in pawn.SkillCooldowns) {
            if (cd.SkillId == skillKey.Id && cd.Remaining > remaining)
                remaining = cd.Remaining;
        }
        return remaining;
    }
}
