using Godot;
using System;

namespace GameLogic {
    [GlobalClass]
    public partial class CircularRange_Res : Range_Res_Base {


        [Export]
        float nearClamp = 0.0f;
        public float NearClamp => nearClamp;
        [Export]
        float farClamp = 1.0f;
        public float FarClamp => farClamp;

        [Export]
        float radianFrom = -1.0f;
        public float RadianFrom => radianFrom;
        [Export]
        float radianTo = 1.0f;
        public float RadianTo => radianTo;

        public override bool IsInRange(UnitState callSkillObject, UnitState testObject, Vector3 targetPos) {
            return Utility.IsInRange_Circular(
                testObject.Position,
                callSkillObject.Position,
                targetPos - callSkillObject.Position,
                nearClamp,
                farClamp,
                radianFrom,
                radianTo
            );
        }

    }
}
