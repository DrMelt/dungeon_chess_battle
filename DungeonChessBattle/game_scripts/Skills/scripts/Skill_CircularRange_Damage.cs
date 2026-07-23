using Godot;


namespace DungeonChessBattle.Core;

public partial class Skill_CircularRange_Damage : UnitSkillBaseGodot {

    [Export]
    float damage = 0;
    [Export]
    Enum_DamageType enum_DamageType;

    [Export]
    float nearClamp = 1.0f;
    [Export]
    float farClamp = 1.0f;
    [Export]
    float radianFrom = -1.0f;
    [Export]
    float radianTo = 1.0f;


    protected override void CallSpelledSkill() {
        foreach (IUnitState skillObject in TestObjects) {
            Utility.IsInRange_Circular(
                skillObject.Position,
                CallSkillObject.Position,
                TargetPos - skillObject.Position,
                nearClamp,
                farClamp,
                radianFrom,
                radianTo);


        }
    }

}
