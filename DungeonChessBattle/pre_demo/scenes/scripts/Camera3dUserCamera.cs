using Godot;

namespace DungeonChessBattle;

public partial class Camera3dUserCamera : Camera3D {
    [Export]
    float _cameraMoveSpeed = 1;

    [Export]
    float _zoomSpeedScale = 50.0f;
    float ZoomSpeed => _zoomSpeedScale * _cameraMoveSpeed;
    [Export]
    float _rotateSpeedScale = 0.01f;
    float RotateSpeed => _rotateSpeedScale * _cameraMoveSpeed;
    [Export]
    float _moveSpeedScale = 1.0f;
    float MoveSpeed => _moveSpeedScale * _cameraMoveSpeed;

    [Export]
    UserInterfaceRes userInterfaceRes;


    Vector2 mousePos;

    public override void _Process(double delta) {

        Vector2 currentMouse = GetViewport().GetMousePosition() * GetViewport().GetVisibleRect().Size;
        if (Input.IsActionPressed("Camera_Rotate")) {
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.001f * RotateSpeed;

            Vector3 cameraPreDir = -GlobalTransform.Basis.Z;
            Basis preBais = GlobalTransform.Basis;

            Vector3 centerPos = GlobalPosition + cameraPreDir * (GlobalPosition.Y / -cameraPreDir.Y);

            UnitGameShow focusOn = userInterfaceRes.FocusOnUnit;
            if (focusOn != null) {
                centerPos = focusOn.GlobalPosition;
            }

            // 1. 绕世界 Y 轴旋转（偏航/Yaw），不受当前俯仰角影响，避免万向节锁定
            Basis yawBasis = new(Vector3.Up, -deltaMouse.X);
            GlobalTransform = new Transform3D(
                (yawBasis * GlobalTransform.Basis).Orthonormalized(),
                GlobalPosition
            );

            // 2. 绕本地 X 轴旋转（俯仰/Pitch），不限制角度，可到达并跨越 90°
            float pitchDelta = -deltaMouse.Y;
            Basis pitchBasis = new(GlobalTransform.Basis.X, pitchDelta);
            GlobalTransform = new Transform3D(
                (pitchBasis * GlobalTransform.Basis).Orthonormalized(),
                GlobalPosition
            );

            // 3. 调整位置，保持相机围绕中心点旋转
            Vector3 vecTo = centerPos - GlobalPosition;
            Basis rotation = GlobalTransform.Basis * preBais.Inverse();
            Vector3 newVec = rotation * vecTo;
            GlobalPosition = centerPos - newVec;

        }

        if (Input.IsActionPressed("Camera_Move")) {
            Vector3 global_X = GlobalTransform.Basis.X;
            Vector3 global_Y = GlobalTransform.Basis.Y;
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.0001f * MoveSpeed;

            GlobalPosition += -global_X * deltaMouse.X + global_Y * deltaMouse.Y;
        }


        mousePos = currentMouse;

        Vector3 cameraDir = -GlobalTransform.Basis.Z;
        if (Input.IsActionJustPressed("Camera_RollBack")) {
            GlobalPosition += -cameraDir * ZoomSpeed * (float)delta;
        }
        if (Input.IsActionJustPressed("Camera_RollUp")) {
            GlobalPosition += cameraDir * ZoomSpeed * (float)delta;
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
