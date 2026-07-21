using Godot;


namespace GameLogic {
    public partial class Skill_CircularRange_Damage : UnitSkillBase {

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
            foreach (IUnitState skillObject in testObjects) {
                Utility.IsInRange_Circular(
                    skillObject.Position,
                    callSkillObject.Position,
                    targetPos - skillObject.Position,
                    nearClamp,
                    farClamp,
                    radianFrom,
                    radianTo);


            }
        }

    }

}
