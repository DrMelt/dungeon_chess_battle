using GameLogic;
using Godot;
using Godot.Collections;
using System;


public partial class StateBar2d : Control
{
    [Export]
    UserOperationInterfaceInfo userOperationInterfaceInfoRef;


    [ExportGroup("Internal Parameters")]
    [Export]
    UserUISettings userUISettingsRef;


    [ExportSubgroup("Buffs")]
    [Export]
    HBoxContainerBuffs hboxContainerBuffsRef;

    [ExportSubgroup("State Bar")]
    [Export]
    PanelFocusState panelFocusStateRef;


    public override void _Process(double delta)
    {
        if (!Engine.IsEditorHint())
        {
            Visible = false;
            UnitGameShow showUnit = GetUnitShow();

            if (showUnit != null)
            {
                Visible = true;

                hboxContainerBuffsRef.UpdateUI_WithUnit(showUnit);
                panelFocusStateRef.UpdateUI_WithUnit(showUnit);
            }

        }
    }


    UnitGameShow GetUnitShow()
    {
        UnitGameShow showUnit = null;
        UnitGameShow mouseOnUnit = userOperationInterfaceInfoRef.MouseOnUnit;
        UnitGameShow focusOnUnit = userOperationInterfaceInfoRef.FocusOnUnit;
        if (mouseOnUnit != null)
        {
            showUnit = mouseOnUnit;
        }
        else if (focusOnUnit != null)
        {
            showUnit = focusOnUnit;
        }
        return showUnit;
    }
}
