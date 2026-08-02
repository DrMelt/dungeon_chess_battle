using Godot;

namespace DungeonChessBattle;

public partial class StateBar2d_Focus : Control {

    public StateBar2d_FocusInterRefs? InterRefs {
        get; private set;
    }

    public override void _Ready() {
        InterRefs = GetNode<StateBar2d_FocusInterRefs>("StateBar2d_FocusInterRefs");
    }

    public override void _Process(double delta) {
        if (!Engine.IsEditorHint()) {
            Visible = false;
            if (InterRefs == null)
                return;
            UnitGameShow? showUnit = GetUnitShow();

            if (showUnit != null) {
                Visible = true;

                InterRefs.HboxContainerBuffsRef?.UpdateUI_WithUnit(showUnit.UnitStateRec);
                InterRefs.PanelFocusStateRef?.UpdateUI_WithUnit(showUnit.UnitStateRec);
                InterRefs.PanelSkillProgressBarRef?.UpdateUI_WithUnit(showUnit.UnitStateRec);
            }

        }
    }


    UnitGameShow? GetUnitShow() {
        UnitGameShow? showUnit = null;
        var uiRes = InterRefs?.UserInterfaceRes;
        if (uiRes == null)
            return null;
        UnitGameShow? mouseOnUnit = uiRes.MouseOnUnit;
        UnitGameShow? focusOnUnit = uiRes.FocusOnUnit;
        if (mouseOnUnit != null) {
            showUnit = mouseOnUnit;
        }
        else if (focusOnUnit != null) {
            showUnit = focusOnUnit;
        }
        return showUnit;
    }
}
