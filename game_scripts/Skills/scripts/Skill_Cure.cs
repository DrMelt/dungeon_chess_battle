using Godot;


namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Cure : UnitSkillBaseGodot {
    [Export]
    float curePotency = 0;

    protected override void CallSpelledSkill() {
        float health = CallSkillObject.CureAmount(curePotency);
        TargetObject.RestoreHealth(health);
    }
}
