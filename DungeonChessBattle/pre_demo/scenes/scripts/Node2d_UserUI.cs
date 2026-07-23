using Godot;
using System.Collections.Generic;

namespace DungeonChessBattle;

public partial class Node2d_UserUI : Control {
    [Export]
    UserInterfaceRes userInterfaceRes;

    [Export]
    UnitsInScene_Show unitsInGameRef;

    [ExportGroup("Internal")]
    [Export]
    SkillsList skillsListRef;
    [Export]
    StateChangeInfo stateChangeInfoRef;
    [Export]
    StateBarList stateBarListRef;

    bool isMouseOn = false;
    public bool IsMouseOn => isMouseOn;

    public override void _Ready() {
        MouseEntered += () => {
            isMouseOn = true;
        };
        MouseExited += () => {
            isMouseOn = false;
        };

        userInterfaceRes.FocusOnUnitChangedEvent += UpdateSkillList;
        UpdateSkillList(userInterfaceRes.FocusOnUnit);

        UpdateBinding();
    }

    public void UpdateBinding() {
        stateChangeInfoRef.BindUnitsInScene(unitsInGameRef.UnitsInSceneRes);
        stateBarListRef.BindUnitsInScene(unitsInGameRef.UnitsInSceneRes);
    }

    public void UpdateSkillList(UnitGameShow unitShow) {
        skillsListRef.UpdateSkillsList(unitShow);
    }


    public bool IsWaitSkillTarget() {
        return skillsListRef.IsWaitTarget();
    }
    public List<ButtonSkillBase> WaitingTargetSkillList() {
        return skillsListRef.WaitingTargetSkillList();
    }

    public static bool IsWaitMoveTarget() {
        return false;
    }
}
