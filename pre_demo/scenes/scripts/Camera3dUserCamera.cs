using Godot;
using System;
using System.Threading;

public partial class Camera3dUserCamera : Camera3D {
    [Export]
    float _cameraMoveSpeed = 1;

    [Export]
    float _zoomSpeedScale = 15.0f;
    float zoomSpeed => _zoomSpeedScale * _cameraMoveSpeed;
    [Export]
    float _rotateSpeedScale = 0.01f;
    float rotateSpeed => _rotateSpeedScale * _cameraMoveSpeed;
    [Export]
    float _moveSpeedScale = 1.0f;
    float moveSpeed => _moveSpeedScale * _cameraMoveSpeed;

    [Export]
    UserInterfaceRes userInterfaceRes;


    Vector2 mousePos;

    public override void _Process(double delta) {

        Vector2 currentMouse = GetViewport().GetMousePosition() * GetViewport().GetVisibleRect().Size;
        if (Input.IsActionPressed("Camera_Rotate")) {
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.001f * (float)delta * rotateSpeed;

            Vector3 cameraPreDir = -GlobalTransform.Basis.Z;
            Basis preBais = GlobalTransform.Basis;

            Vector3 centerPos = GlobalPosition + cameraPreDir * (GlobalPosition.Y / -cameraPreDir.Y);

            UnitGameShow focusOn = userInterfaceRes.FocusOnUnit;
            if (focusOn != null) {
                centerPos = focusOn.GlobalPosition;
            }


            Rotation = Rotation + new Vector3(-deltaMouse.Y, -deltaMouse.X, 0);

            Vector3 vecTo = centerPos - GlobalPosition;
            Basis rotation = GlobalTransform.Basis * preBais.Inverse();
            Vector3 newVec = rotation * vecTo;
            GlobalPosition = centerPos - newVec;

        }

        if (Input.IsActionPressed("Camera_Move")) {
            Vector3 global_X = GlobalTransform.Basis.X;
            Vector3 global_Y = GlobalTransform.Basis.Y;
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.0001f * (float)delta * moveSpeed;

            GlobalPosition += -global_X * deltaMouse.X + global_Y * deltaMouse.Y;
        }


        mousePos = currentMouse;

        Vector3 cameraDir = -GlobalTransform.Basis.Z;
        if (Input.IsActionJustPressed("Camera_RollBack")) {
            GlobalPosition += -cameraDir * zoomSpeed * (float)delta;
        }
        if (Input.IsActionJustPressed("Camera_RollUp")) {
            GlobalPosition += cameraDir * zoomSpeed * (float)delta;
        }



        if (Input.IsActionJustPressed("Camera_MoveToFocus")) {
            UnitGameShow focusOn = userInterfaceRes.FocusOnUnit;
            if (focusOn != null) {
                Vector3 vecToFocus = focusOn.GlobalPosition - GlobalPosition;
                float projectValue = Mathf.Abs(vecToFocus.Dot(cameraDir));
                GlobalPosition = focusOn.GlobalPosition - cameraDir * projectValue;
            }
        }
        if (Input.IsActionJustPressed("Camera_TopView")) {
            LookAt(GlobalPosition + new Vector3(0, -1, 0));
        }
    }


}
