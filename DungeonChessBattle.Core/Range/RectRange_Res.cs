using Godot;
using System;

namespace DungeonChessBattle.Core.Range {
    [GlobalClass]
    public partial class RectRange_Res : Range_Res_Base {
        [Export]
        readonly float nearClamp = 0.0f;
        public float NearClamp => nearClamp;

        [Export]
        readonly float farClamp = 1.0f;
        public float FarClamp => farClamp;

        [Export]
        readonly float fromL = -1.0f;
        public float FromL => fromL;

        [Export]
        readonly float toR = 1.0f;
        public float ToR => toR;

        public override bool IsInRange(DungeonChessBattle.Core.Interfaces.IUnitState callSkillObject, DungeonChessBattle.Core.Interfaces.IUnitState testObject, Vector3 targetPos) {
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
