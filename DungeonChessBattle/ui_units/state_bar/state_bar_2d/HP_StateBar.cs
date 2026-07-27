using DungeonChessBattle.ui_units.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class HP_StateBar : Control, IUI_Update {
    [ExportGroup("References")]
    [Export]
    UserUISettings userUISettingsRef = null!;

    [Export]
    CanvasItem stateBarRef = null!;
    ShaderMaterial stateBarRef_Mat = null!;


    [Export]
    Label label_PercentRef = null!;
    [Export]
    Label label_CurrentValueRef = null!;
    [Export]
    Label label_ObjectNameRef = null!;

    public override void _Ready() {
        stateBarRef_Mat = (stateBarRef.Material as ShaderMaterial)!;
    }

    public void UpdateUI_WithUnit(UnitState unitState) {
        if (unitState == null) {
            return;
        }

        Color? campColor = userUISettingsRef.GetCampColor(unitState.Camp);
        if (campColor != null) {
            stateBarRef_Mat.SetShaderParameter("ParPin_01_Color", (Color)campColor);
        }
        stateBarRef_Mat.SetShaderParameter("ParPin_01", unitState.Health_Percent);


        label_PercentRef.Text = unitState.Health_Shield_Percent.ToString("P1");
        label_CurrentValueRef.Text = unitState.Health_Shield.ToString("F1");

        label_ObjectNameRef.Text = unitState.UnitStateName;
    }

}
