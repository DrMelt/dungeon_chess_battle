using DungeonChessBattle.Core.Interfaces;

namespace DungeonChessBattle.Core.Models;

public class SkillAddBuffModel : SkillModel {
    public IBuff Buff { get; set; } = null!;

    protected override void CallSpelledSkill() {
        // 执行逻辑已迁移至 BattleResolver.ApplySkillAddBuff。
    }
}
