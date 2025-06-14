using Godot;
using System;

public partial class Node2d_UserUI : Control
{
    [Export]
    UserOperationInterfaceInfo userOperationInterfaceInfoRef;

    [Export]
    UnitsInGame unitsInGameRef;

    [Export]
    SkillsList skillsListRef;


    bool isMouseOn = false;
    public bool IsMouseOn => isMouseOn;
    public override void _Ready()
    {
        userOperationInterfaceInfoRef.FocusOnUnitChangedEvent += UpdateSkillList;
        UpdateSkillList(userOperationInterfaceInfoRef.FocusOnUnit);

        MouseEntered += () =>
        {
            isMouseOn = true;
        };
        MouseExited += () =>
        {
            isMouseOn = false;
        };
    }

    public bool IsWaitSkillTarget()
    {
        return skillsListRef.IsWaitTarget();
    }

    public bool IsWaitMoveTarget()
    {
        return false;
    }

    public void UpdateSkillList(UnitGameShow unitShow)
    {
        skillsListRef.UpdateSkillsList(unitShow);
    }

}
