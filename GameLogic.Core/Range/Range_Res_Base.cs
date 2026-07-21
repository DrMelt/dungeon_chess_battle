using Godot;
using System;

namespace GameLogic.Range {
    [GlobalClass]
    public partial class Range_Res_Base : Resource {
        public virtual bool IsInRange(GameLogic.Interfaces.IUnitState callSkillObject, GameLogic.Interfaces.IUnitState testObject, Vector3 targetPos) {
            return false;
        }
    }
}
