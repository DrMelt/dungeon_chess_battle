using Godot;
using System;

namespace GameLogic {
    [GlobalClass]
    public partial class RectRange_Res : Range_Res_Base {
        [Export]
        float nearClamp = 0.0f;
        public float NearClamp => nearClamp;

        [Export]
        float farClamp = 1.0f;
        public float FarClamp => farClamp;

        [Export]
        float fromL = -1.0f;
        public float FromL => fromL;

        [Export]
        float toR = 1.0f;
        public float ToR => toR;

        public override bool IsInRange(IUnitState callSkillObject, IUnitState testObject, Vector3 targetPos) {
            return Utility.IsInRange_Rect(
                testObject.Position,
                callSkillObject.Position,
                targetPos - callSkillObject.Position,
                nearClamp,
                farClamp,
                fromL,
                toR
            );
        }
    }
}
