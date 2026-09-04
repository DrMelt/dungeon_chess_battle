using DungeonChessBattle.Battle.Shared.Combat;
using DungeonChessBattle.Game.BattleScene;
using DungeonChessBattle.Game.Services;
using Godot;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.Game.GameAssets;

/// <summary>
/// 用户控制的 3D 相机，支持旋转、平移、缩放、聚焦单位与俯视操作。
/// 进入战斗且本地单位出现后默认锁定跟随，以本地玩家角色为屏幕中心；
/// 可按 Camera_FollowPlayer 键切换自由视角，退出战斗后下次进入恢复默认锁定。
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

    /// <summary>战斗会话上下文引用，用于获取本地单位与聚焦目标 Pawn。</summary>
    [Export]
    private BattleSessionContext? _sessionRef;

    /// <summary>是否锁定跟随本地单位。</summary>
    private bool _followPlayerEnabled;

    /// <summary>锁定跟随时相机相对本地单位的偏移。</summary>
    private Vector3 _followOffset;

    /// <summary>上一帧是否存在本地单位 Pawn，用于检测进入战斗的上升沿。</summary>
    private bool _hadLocalUnit;

    /// <summary>上一帧的鼠标位置。</summary>
    private Vector2 mousePos;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (_sessionRef == null)
            _logger.LogError("_sessionRef is not assigned!");
    }

    /// <summary>
    /// 每帧处理相机旋转、平移、聚焦、俯视与锁定跟随指令。
    /// </summary>
    /// <param name="delta">距上一帧的秒数。</param>
    public override void _Process(double delta) {
        Vector2 currentMouse = GetViewport().GetMousePosition() * GetViewport().GetVisibleRect().Size;

        var localUnit = _sessionRef?.LocalUnit;
        Vector3? localPos = PawnWorldPos(localUnit);

        // 本地单位从无到有视为进入战斗，默认进入锁定并立即居中
        if (localPos != null && !_hadLocalUnit) {
            _followPlayerEnabled = true;
            AlignToLocalUnit(localPos.Value);
        }
        _hadLocalUnit = localPos != null;

        if (_rotationEnabled && Input.IsActionPressed("Camera_Rotate")) {
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.0001f * RotateSpeed;

            Vector3 cameraPreDir = -GlobalTransform.Basis.Z;
            Basis preBais = GlobalTransform.Basis;

            Vector3 centerPos = GlobalPosition + cameraPreDir * (GlobalPosition.Y / -cameraPreDir.Y);

            // 锁定跟随模式下以本地玩家为旋转中心，保证角色始终位于屏幕中心
            var focusUnit = _followPlayerEnabled ? localUnit : _sessionRef?.LocalFocus;
            Vector3? focusPos = PawnWorldPos(focusUnit);
            if (focusPos != null) {
                centerPos = focusPos.Value;
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

            // 旋转后刷新偏移锚，跟随同步时保留环绕视角
            if (_followPlayerEnabled && localPos != null)
                _followOffset = GlobalPosition - localPos.Value;
        }

        // 锁定跟随模式下禁用自由平移，避免破坏居中
        if (!_followPlayerEnabled && Input.IsActionPressed("Camera_Move")) {
            Vector3 global_X = GlobalTransform.Basis.X;
            Vector3 global_Y = GlobalTransform.Basis.Y;
            float sizeScaledSpeed = MoveSpeed * Size * 0.1f;
            Vector2 deltaMouse = (currentMouse - mousePos) * 0.0001f * sizeScaledSpeed;

            GlobalPosition += -global_X * deltaMouse.X + global_Y * deltaMouse.Y;
        }

        mousePos = currentMouse;

        Vector3 cameraDir = -GlobalTransform.Basis.Z;

        // 锁定跟随模式下已居中，跳过聚焦移动
        if (!_followPlayerEnabled && Input.IsActionJustPressed("Camera_MoveToFocus")) {
            Vector3? focusPos = PawnWorldPos(_sessionRef?.LocalFocus);
            if (focusPos != null) {
                Vector3 vecToFocus = focusPos.Value - GlobalPosition;
                float projectValue = Mathf.Abs(vecToFocus.Dot(cameraDir));
                GlobalPosition = focusPos.Value - cameraDir * projectValue;
            }
        }
        if (Input.IsActionJustPressed("Camera_TopView")) {
            LookAt(GlobalPosition + new Vector3(0, -1, 0));
        }

        // 锁定跟随：以本地单位位置为屏幕中心同步相机
        if (_followPlayerEnabled && localPos != null)
            GlobalPosition = localPos.Value + _followOffset;

        // 切换锁定跟随，切回时立即归中
        if (Input.IsActionJustPressed("Camera_FollowPlayer")) {
            _followPlayerEnabled = !_followPlayerEnabled;
            if (_followPlayerEnabled && localPos != null)
                AlignToLocalUnit(localPos.Value);
        }
    }

    /// <summary>从本地展示视图位置派生场景坐标（与 UnitGameShow 同一投影公式）。</summary>
    private static Vector3? PawnWorldPos(IUnitUiView? unit)
        => unit == null
            ? null
            : new Vector3(unit.Position.X, 0f, unit.Position.Y);

    /// <summary>
    /// 锁定归中：相机 XZ 对齐本地单位并保留高度，使角色处于视野中心。
    /// </summary>
    private void AlignToLocalUnit(Vector3 unitPos) {
        _followOffset = new Vector3(0, GlobalPosition.Y - unitPos.Y, 0);
        GlobalPosition = unitPos + _followOffset;
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
