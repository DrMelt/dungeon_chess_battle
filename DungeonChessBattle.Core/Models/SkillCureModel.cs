namespace DungeonChessBattle.Core.Models;

public class SkillCureModel : SkillModel {
    public float CurePotency {
        get; set;
    }

    protected override void CallSpelledSkill() {
        float health = CallSkillObject.CureAmount(CurePotency);
        TargetObject.RestoreHealth(health);
    }
}
