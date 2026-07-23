using DungeonChessBattle.Core;
using Godot;

namespace DungeonChessBattle {

    public partial class StateBar : Node3D, IUI_Update {
        [Export]
        float scaleBase = 0.5f;
        [Export]
        float scaleCamera = 0.3f;


        [ExportGroup("Internal Parameters")]
        [Export]
        UserUISettings userUISettingsRef;

        [Export]
        MeshInstance3D stateBarRef;
        ShaderMaterial stateBarRef_Mat;

        [Export]
        Label3D label3D_PercentRef;
        [Export]
        Label3D label3D_CurrentValueRef;
        [Export]
        Label3D label3D_NameRef;
        public override void _Ready() {
            stateBarRef_Mat = stateBarRef.MaterialOverride as ShaderMaterial;
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
            if (unitState == null) {
                return;
            }

            Color? campColor = userUISettingsRef.GetCampColor(unitState.Camp);
            if (campColor != null) {
                stateBarRef_Mat.SetShaderParameter("ParPin_01_Color", (Color)campColor);
            }

            stateBarRef_Mat.SetShaderParameter("ParPin_01", unitState.Health_Percent);
            label3D_PercentRef.Text = unitState.Health_Shield_Percent.ToString("P1");
            label3D_CurrentValueRef.Text = unitState.Health_Shield.ToString("F1");
            label3D_NameRef.Text = unitState.UnitStateName;
        }

    }
}
