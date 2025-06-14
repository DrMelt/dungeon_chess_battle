using Godot;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;


namespace GameLogic
{
    [GlobalClass]
    public partial class UnitSkillBase : Resource
    {
        [Export]
        string skillName = "UnitSkillBaseName";
        public string SkillName => skillName;

        [Export(PropertyHint.MultilineText)]
        string skillDescription = "UnitSkillBaseDescription";
        public string SkillDescription => skillDescription;

        [Export]
        float skillRange = 10.0f;

        [Export]
        float skillSpringTime = 0.5f;

        [Export]
        float skillCooldownTime = 0.5f;

        [Export]
        Texture2D icon;
        public Texture2D Icon => icon;

        [Export]
        bool needUnitTarget = false;
        public bool NeedUnitTarget => needUnitTarget;

        [Export]
        bool needPosTarget = false;
        public bool NeedPosTarget => needPosTarget;

        public virtual void CallSkill(UnitState callSkillObject, UnitState targetObject, Vector3? targetPos, List<UnitState> allObjects)
        {
            GD.Print($"{skillName} is called to {targetObject}");
        }

    }
}
