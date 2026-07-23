using Godot;


namespace DungeonChessBattle.Core;

[GlobalClass]
public partial class Skill_Add_BUFF : UnitSkillBaseGodot {
    [Export]
    BuffBaseGodot buff;


    protected override void CallSpelledSkill() {
        BuffBaseGodot addBuff = buff.Duplicate() as BuffBaseGodot;
        addBuff.fromUnit = CallSkillObject;
        TargetObject.AddBuff(addBuff);
    }
}
