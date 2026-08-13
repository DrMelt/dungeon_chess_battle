using DungeonChessBattle.GamePlayUI;
using DungeonChessBattle.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.GameAssets;

/// <summary>
/// 用户控制的 3D 相机，支持旋转、平移、缩放、聚焦单位与俯视操作。
/// </summary>
public partial class UserCamera3D : Camera3D {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<UserCamera3D> _logger = ServiceLocator.GetLogger<UserCamera3D>();

    /// <summary>相机基准移动速度。</summary>
    [Export]
    private float _cameraMoveSpeed = 1;

    /// <summary>滚轮缩放的倍率系数。</summary>
    [Export]
    private float _zoomSpeedScale = 10.0f;
    /// <summary>缩放速度。</summary>
    private float ZoomSpeed => _zoomSpeedScale * _cameraMoveSpeed;

    /// <summary>最小缩放尺寸。</summary>
    [Export]
    private float _zoomMin = 0.5f;

    /// <summary>最大缩放尺寸。</summary>
    [Export]
    private float _zoomMax = 50.0f;

    /// <summary>旋转速度倍率系数。</summary>
    [Export]
    private float _rotateSpeedScale = 0.1f;
    /// <summary>旋转速度。</summary>
    private float RotateSpeed => _rotateSpeedScale * _cameraMoveSpeed;

    /// <summary>移动速度倍率系数。</summary>
    [Export]
    private float _moveSpeedScale = 1.0f;
    /// <summary>平移速度。</summary>
    private float MoveSpeed => _moveSpeedScale * _cameraMoveSpeed;

    /// <summary>是否启用相机旋转。</summary>
    [Export]
    private bool _rotationEnabled = true;

    /// <summary>玩家界面资源引用，用于获取焦点单位。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>上一帧的鼠标位置。</summary>
    private Vector2 mousePos;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (playerInterfaceRes == null)
            _logger.LogError("playerInterfaceRes is not assigned!");
    }

    /// <summary>
    /// 每帧处理相机旋转、平移、聚焦与俯视指令。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        Vector2 currentMouse = GetViewport().GetMousePosition() * GetViewport().GetVisibleRect().Size;
        if (_rotationEnabled && Input.IsActionPressed("Camera_Rotate")) {
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.0001f * RotateSpeed;

            Vector3 cameraPreDir = -GlobalTransform.Basis.Z;
            Basis preBais = GlobalTransform.Basis;

            Vector3 centerPos = GlobalPosition + cameraPreDir * (GlobalPosition.Y / -cameraPreDir.Y);

            UnitGameShow? focusOn = playerInterfaceRes?.FocusOnUnit;
            if (focusOn != null) {
                centerPos = focusOn.GlobalPosition;
            }

            // 1. 绕世界 Y 轴旋转（偏航/Yaw）
            Basis yawBasis = new(Vector3.Up, -deltaMouse.X);
            GlobalTransform = new Transform3D(
                (yawBasis * GlobalTransform.Basis).Orthonormalized(),
                GlobalPosition
            );

            // 2. 绕本地 X 轴旋转（俯仰/Pitch）
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
            float sizeScaledSpeed = MoveSpeed * Size * 0.1f;
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.0001f * sizeScaledSpeed;

            GlobalPosition += -global_X * deltaMouse.X + global_Y * deltaMouse.Y;
        }

        mousePos = currentMouse;

        Vector3 cameraDir = -GlobalTransform.Basis.Z;

        if (Input.IsActionJustPressed("Camera_MoveToFocus")) {
            UnitGameShow? focusOn = playerInterfaceRes?.FocusOnUnit;
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

    /// <summary>
    /// 处理滚轮缩放输入，并限制缩放范围。
    /// </summary>
    /// <param name="event">输入事件。</param>
    public override void _UnhandledInput(InputEvent @event) {
        if (@event is InputEventMouseButton mouseButton) {
            float scaleFactor = 1.0f + ZoomSpeed * 0.01f;
            if (mouseButton.ButtonIndex == MouseButton.WheelDown) {
                Size *= scaleFactor;
            }
            if (mouseButton.ButtonIndex == MouseButton.WheelUp) {
                Size /= scaleFactor;
            }
            Size = Mathf.Clamp(Size, _zoomMin, _zoomMax);
        }
    }
}
