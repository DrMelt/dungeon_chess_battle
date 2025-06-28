using Godot;
using System;

namespace GameLogic {
    [GlobalClass]
    public partial class Range_Res_Base : Resource {

        public virtual bool IsInRange(UnitState callSkillObject, UnitState testObject, Vector3 targetPos) {
            return false;
        }
    }
}
