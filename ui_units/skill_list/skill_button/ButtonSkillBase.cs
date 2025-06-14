using GameLogic;
using Godot;
using System;

public partial class ButtonSkillBase : Button
{
    UnitSkillBase bindSkill;
    public UnitSkillBase BindSkill => bindSkill;
    UnitState bindUnitState;
    public UnitState BindUnitState => bindUnitState;

    SkillsList skillsListRef;


    public void Init(UnitSkillBase bindSkill, UnitState bindUnitState, SkillsList skillsListRef)
    {
        this.bindSkill = bindSkill;
        this.bindUnitState = bindUnitState;
        this.skillsListRef = skillsListRef;

        if (bindSkill.Icon != null)
        {
            Icon = bindSkill.Icon;
        }
    }

    public override void _Ready()
    {
        MouseEntered += () =>
        {
            skillsListRef.PanelSkillInfoRef.MouseOnButtonSkill = this;
        };

        MouseExited += () =>
        {
            if (skillsListRef.PanelSkillInfoRef.MouseOnButtonSkill == this)
            {
                skillsListRef.PanelSkillInfoRef.MouseOnButtonSkill = null;
            }
        };
    }

    bool waitTarget = false;
    public bool WaitTarget => waitTarget;
    public override void _Pressed()
    {
        if (bindSkill.NeedUnitTarget == false)
        {
            bindSkill.CallSkill(bindUnitState, null, null, skillsListRef.UnitsInGameRef.GetUnitsStateList());
        }
        else
        {
            waitTarget = true;
        }
    }


    public override void _Process(double delta)
    {
        if (!waitTarget)
        {
            return;
        }

        if (Input.IsActionJustPressed("Skill_SelectTarget"))
        {
            UnitGameShow mouseOn = skillsListRef.UserOperationInterfaceInfoRef.MouseOnUnit;
            if (mouseOn != null)
            {
                bindSkill.CallSkill(bindUnitState, mouseOn.UnitStateRec, null, skillsListRef.UnitsInGameRef.GetUnitsStateList());
            }
            waitTarget = false;
            ButtonPressed = false;
        }
    }





}
