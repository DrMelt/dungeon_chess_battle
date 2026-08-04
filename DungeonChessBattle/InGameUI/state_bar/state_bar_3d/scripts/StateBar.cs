using System.Linq;
using DungeonChessBattle.InGameUI.ui_interface;
using Godot;

namespace DungeonChessBattle;

public partial class StateBar : Node3D, IUI_Update {
    [Export]
    float scaleBase = 0.5f;
    [Export]
    float scaleCamera = 0.3f;

    public StateBarInterRefs? InterRefs {
        get; private set;
    }
    ShaderMaterial? stateBarMat;

    public override void _Ready() {
        InterRefs = GetNode<StateBarInterRefs>("StateBarInterRefs");
        if (InterRefs?.StateBarRef?.MaterialOverride is ShaderMaterial mat) {
            stateBarMat = mat;
        }
    }

    public override void _Process(double delta) {
        LookAtCamera();
    }

    private void LookAtCamera() {
        Camera3D camera3D = GetViewport().GetCamera3D();
        if (camera3D != null) {
            Vector3 cameraPos = camera3D.GlobalPosition;
            LookAt(cameraPos, camera3D.Basis.Y);

            float cameraLen = (cameraPos - GlobalPosition).Length();
            float newScale = cameraLen * scaleCamera + scaleBase;
            Scale = new Vector3(newScale, newScale, 1);
        }
    }


    public void UpdateUI_WithUnit(UnitState unitState) {
        if (unitState == null || InterRefs == null) {
            return;
        }

        if (stateBarMat != null) {
            Color? campColor = InterRefs.UserUISettingsRef?.GetCampColor(unitState.Camps.FirstOrDefault() ?? "");
            if (campColor != null) {
                stateBarMat.SetShaderParameter("ParPin_01_Color", (Color)campColor);
            }
            stateBarMat.SetShaderParameter("ParPin_01", unitState.Health_Percent);
        }

        InterRefs.Label3DPercentRef?.Text = unitState.Health_Shield_Percent.ToString("P1");
        InterRefs.Label3DCurrentValueRef?.Text = unitState.Health_Shield.ToString("F1");
        InterRefs.Label3DNameRef?.Text = unitState.UnitStateName;
    }

}
