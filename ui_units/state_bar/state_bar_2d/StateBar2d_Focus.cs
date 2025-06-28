using Godot;
using Godot.Collections;
using System;

using GameLogic;

public partial class StateBar2d_Focus : Control {

    [Export]
    UserInterfaceRes userInterfaceRes;


    [ExportGroup("Internal Parameters")]
    [Export]
    UserUISettings userUISettingsRef;


    [ExportSubgroup("Buffs")]
    [Export]
    ContainerBuffs hboxContainerBuffsRef;

    [ExportSubgroup("State Bar")]
    [Export]
    HP_StateBar panelFocusStateRef;

    [Export]
    SkillProgressBar panelSkillProgressBarRef;


    public override void _Process(double delta) {
        if (!Engine.IsEditorHint()) {
            Visible = false;
            UnitGameShow showUnit = GetUnitShow();

            if (showUnit != null) {
                Visible = true;

                hboxContainerBuffsRef.UpdateUI_WithUnit(showUnit.UnitStateRec);
                panelFocusStateRef.UpdateUI_WithUnit(showUnit.UnitStateRec);
                panelSkillProgressBarRef.UpdateUI_WithUnit(showUnit.UnitStateRec);
            }

        }
    }


    UnitGameShow GetUnitShow() {
        UnitGameShow showUnit = null;
        UnitGameShow mouseOnUnit = userInterfaceRes.MouseOnUnit;
        UnitGameShow focusOnUnit = userInterfaceRes.FocusOnUnit;
        if (mouseOnUnit != null) {
            showUnit = mouseOnUnit;
        }
        else if (focusOnUnit != null) {
            showUnit = focusOnUnit;
        }
        return showUnit;
    }
}
