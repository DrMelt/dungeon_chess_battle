using Godot;
using System.Collections.Generic;

namespace DungeonChessBattle;

public partial class Node2d_UserUI : Control {
    [Export]
    UserInterfaceRes userInterfaceRes = null!;

    [Export]
    UnitsInScene_Show unitsInGameRef = null!;

    [ExportGroup("Internal")]
    [Export]
    SkillsList skillsListRef = null!;

    [Export]
    StateChangeInfo stateChangeInfoRef = null!;

    [Export]
    StateBarList stateBarListRef = null!;

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
        UpdateSkillList(userInterfaceRes.FocusOnUnit!);

        UpdateBinding();
    }

    public void UpdateBinding() {
        if (unitsInGameRef != null) {
            stateChangeInfoRef?.BindUnitsInScene(unitsInGameRef.UnitsInSceneRes);
            stateBarListRef?.BindUnitsInScene(unitsInGameRef.UnitsInSceneRes);
        }
    }

    public void UpdateSkillList(UnitGameShow unitShow) {
        skillsListRef?.UpdateSkillsList(unitShow);
    }

    public bool IsWaitSkillTarget() {
        return skillsListRef != null && skillsListRef.IsWaitTarget();
    }

    public List<ButtonSkillBase> WaitingTargetSkillList() {
        return skillsListRef?.WaitingTargetSkillList() ?? [];
    }

    public static bool IsWaitMoveTarget() {
        return false;
    }
}
