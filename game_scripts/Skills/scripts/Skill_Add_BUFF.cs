using Godot;
using System;
using System.Collections.Generic;


namespace GameLogic {
    [GlobalClass]
    public partial class Skill_Add_BUFF : UnitSkillBase {
        [Export]
        BuffBase buff;


        protected override void CallSpelledSkill() {
            BuffBase addBuff = buff.Duplicate() as BuffBase;
            addBuff.fromUnit = callSkillObject;
            targetObject.AddBuff(addBuff);
        }
    }
}
