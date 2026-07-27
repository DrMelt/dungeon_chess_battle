using DungeonChessBattle.Core.Enums;
using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class SkillRangeDamageModel : SkillModel {
    public float Damage {
        get; set;
    }
    public Enum_DamageType DamageType {
        get; set;
    }
    public IRangeRes RangeRes { get; set; } = null!;

    protected override void CallSpelledSkill() {
        // 执行逻辑已迁移至 BattleResolver.ApplySkillRangeDamage。
    }
}
