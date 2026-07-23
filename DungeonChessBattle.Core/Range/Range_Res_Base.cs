using Godot;
using System;

namespace DungeonChessBattle.Core.Range {
    [GlobalClass]
    public partial class Range_Res_Base : Resource {
        public virtual bool IsInRange(DungeonChessBattle.Core.Interfaces.IUnitState callSkillObject, DungeonChessBattle.Core.Interfaces.IUnitState testObject, Vector3 targetPos) {
            return false;
        }
    }
}
