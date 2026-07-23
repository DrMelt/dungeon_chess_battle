using Godot;

namespace DungeonChessBattle;

public partial class UserOperationInterfaceInfo : Node {
    [Export]
    Camera3D camera3DRef;
    [Export]
    Node2d_UserUI node2dUiRef;

    [Export]
    UserInterfaceRes userInterfaceRes;


    [ExportGroup("Intrinsic Parameter")]
    [Export]
    EffectHints effectHintsRef;

    [Export]
    RayCast3D rayCast3DRef;



    public override void _PhysicsProcess(double delta) {
        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 from = camera3DRef.ProjectRayOrigin(mousePos);
        rayCast3DRef.Position = from;
        rayCast3DRef.TargetPosition = camera3DRef.ProjectRayNormal(mousePos) * 1000.0f;

        bool isSetted = false;
        if (rayCast3DRef.IsColliding()) {
            if (rayCast3DRef.GetCollider() is UnitShowArea3D area3D) {
                userInterfaceRes.MouseOnUnit = area3D.UnitShowRef;
                isSetted = true;
            }
        }

        if (!isSetted) {
            userInterfaceRes.MouseOnUnit = null;
        }

        if (rayCast3DRef.GetCollider() is StaticBody3D staticBody3D) {
            if (staticBody3D.GetCollisionLayerValue(6)) {
                userInterfaceRes.MouseGoundPosition = rayCast3DRef.GetCollisionPoint();
            }
            else {
                userInterfaceRes.MouseGoundPosition = null;
            }
        }
    }


    public override void _Process(double delta) {
        // User Operation
        if (node2dUiRef.IsWaitSkillTarget() || node2dUiRef.IsMouseOn) {
            return;
        }

        if (Input.IsActionJustPressed("Move_SelectMoveTarget")) {
            if (userInterfaceRes.FocusOnUnit != null && userInterfaceRes.MouseGoundPosition != null) {
                userInterfaceRes.FocusOnUnit.SetMoveTarget(userInterfaceRes.MouseGoundPosition.Value);
            }
        }
        else if (Input.IsActionJustPressed("Interface_SelectFocus")) {
            userInterfaceRes.FocusOnUnit = userInterfaceRes.MouseOnUnit;
        }





    }

}
