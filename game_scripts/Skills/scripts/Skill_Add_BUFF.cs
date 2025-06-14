using Godot;
using System;
using System.Collections.Generic;


namespace GameLogic {
    [GlobalClass]
    public partial class Skill_Add_BUFF : UnitSkillBase {
        [Export]
        BuffBase buff;

        public override void CallSkill(UnitState callObject, UnitState targetObject, Vector3? targetPos, List<UnitState> skillObjects) {
            BuffBase addBuff = buff.Duplicate() as BuffBase;
            addBuff.fromUnit = callObject;
            targetObject.AddBuff(addBuff);
        }
    }
}
