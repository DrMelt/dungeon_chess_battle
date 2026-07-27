namespace DungeonChessBattle.Core.Models;

public class SkillCureModel : SkillModel {
    public float CurePotency {
        get; set;
    }

    protected override void CallSpelledSkill() {
        // 执行逻辑已迁移至 BattleResolver.ApplySkillCure。
    }
}
