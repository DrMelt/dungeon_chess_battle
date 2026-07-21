using Godot;
using System;

namespace GameLogic.Range {
    [GlobalClass]
    public partial class CircularRange_Res : Range_Res_Base {
        [Export]
        readonly float nearClamp = 0.0f;
        public float NearClamp => nearClamp;

        [Export]
        readonly float farClamp = 1.0f;
        public float FarClamp => farClamp;

        [Export]
        readonly float radianFrom = -1.0f;
        public float RadianFrom => radianFrom;

        [Export]
        readonly float radianTo = 1.0f;
        public float RadianTo => radianTo;

        public override bool IsInRange(GameLogic.Interfaces.IUnitState callSkillObject, GameLogic.Interfaces.IUnitState testObject, Vector3 targetPos) {
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
