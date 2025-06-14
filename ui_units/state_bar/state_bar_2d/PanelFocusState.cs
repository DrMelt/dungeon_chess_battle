using Godot;
using System;

public partial class PanelFocusState : Panel, I_UI_Update
{
    [ExportGroup("References")]
    [Export]
    UserUISettings userUISettingsRef;

    [Export]
    CanvasItem stateBarRef;
    ShaderMaterial stateBarRef_Mat;


    [Export]
    Label label_PercentRef;
    [Export]
    Label label_CurrentValueRef;
    [Export]
    Label label_ObjectNameRef;

    public override void _Ready()
    {
        stateBarRef_Mat = stateBarRef.Material as ShaderMaterial;
    }

    public void UpdateUI_WithUnit(UnitGameShow unitShow)
    {
        if (unitShow == null)
        {
            return;
        }

        Color? campColor = userUISettingsRef.GetCampColor(unitShow.UnitStateRec.Camp);
        if (campColor != null)
        {
            stateBarRef_Mat.SetShaderParameter("ParPin_01_Color", (Color)campColor);
        }

        stateBarRef_Mat.SetShaderParameter("ParPin_01", unitShow.UnitStateRec.Health_Percent);
        label_PercentRef.Text = unitShow.UnitStateRec.Health_Shield_Percent.ToString("P1");
        label_CurrentValueRef.Text = unitShow.UnitStateRec.Health_Shield.ToString("F1");

        label_ObjectNameRef.Text = unitShow.UnitStateRec.UnitStateName;
    }

}
