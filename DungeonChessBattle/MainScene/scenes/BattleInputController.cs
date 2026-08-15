using DungeonChessBattle.Client.Battle;
using DungeonChessBattle.GameAssets;
using DungeonChessBattle.GamePlayUI;
using DungeonChessBattle.Services;
using Godot;
using Godot.Collections;
using Microsoft.Extensions.Logging;

namespace DungeonChessBattle.MainScene;

/// <summary>
/// 战斗输入控制器：采集玩家输入（移动/瞄准）并提交战斗服务，
/// 同时负责 3D 交互拾取（更新鼠标悬停单位/地面瞄准点、左键聚焦目标）写入共享交互状态。
/// 由 MainScene 在每帧 _Process 中调度 Tick；等待目标选择时暂缓提交移动输入。
/// </summary>
public partial class BattleInputController : Node {
    /// <summary>日志记录器。</summary>
    private static readonly ILogger<BattleInputController> _logger = ServiceLocator.GetLogger<BattleInputController>();

    /// <summary>共享交互状态（读等待阻塞、写悬停单位与地面点）。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceResRef;

    /// <summary>战斗单位管理器引用（左键聚焦目标 RPC）。</summary>
    [Export]
    private BattleUnitManager? unitManagerRef;

    /// <summary>单位选择射线最大距离。</summary>
    private const float RaycastMaxDistance = 200f;

    /// <summary>单位交互碰撞层（对应 UnitShowArea3D 的 collision_layer=2048）。</summary>
    private const uint UnitCollisionLayer = 2048;

    /// <summary>地面平面 Y 坐标（场景地面高度）。</summary>
    private const float GroundPlaneY = 0f;

    /// <summary>移动输入向量。</summary>
    private Vector2 _moveDir;

    /// <summary>
    /// 节点就绪：校验导出引用是否已赋值。
    /// </summary>
    public override void _Ready() {
        if (playerInterfaceResRef == null)
            _logger.LogError("playerInterfaceResRef is not assigned!");
        if (unitManagerRef == null)
            _logger.LogError("unitManagerRef is not assigned!");
        if (cameraRef == null)
            _logger.LogError("cameraRef is not assigned!");
    }

    /// <summary>当前活动相机引用。</summary>
    [Export]
    private Camera3D? cameraRef;

    /// <summary>
    /// 每帧采集输入并提交到战斗服务；先更新 3D 拾取，等待目标选择时跳过移动输入提交。
    /// </summary>
    /// <param name="service">当前战斗服务。</param>
    public void Tick(IClientBattleService service) {
        UpdateRaycast();

        // 等待技能目标选择中，暂停提交战斗输入
        if (playerInterfaceResRef?.IsWaitingSkillTarget == true)
            return;

        _moveDir = Input.GetVector("Move_Left", "Move_Right", "Move_Up", "Move_Down");
        service.SubmitPlayerInput(_moveDir.X, _moveDir.Y);
    }

    /// <summary>
    /// 鼠标左键点击：请求设置或清除本地玩家单位的聚焦目标。
    /// 经 RPC 提交服务端，校验后写回服务端权威聚焦目标，
    /// 由 BattleUnitManager 投影为本地焦点单位并派发变化事件。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event) {
        if (@event.IsActionPressed("SwitchTarget")) {
            SwitchToNextEnemy();
            return;
        }

        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            return;
        if (unitManagerRef == null) {
            _logger.LogWarning("unitManagerRef is not assigned!");
            return;
        }

        var hit = RaycastUnitFromCamera();
        var targetNetId = hit?.UnitShowRef.Pawn.Id ?? 0;
        if (_logger.IsEnabled(LogLevel.Debug))
            _logger.LogDebug("Clicked: target={TargetId}, raycastHit={Hit}", targetNetId, hit != null);
        unitManagerRef.SetLocalFocusTarget(targetNetId);
    }

    /// <summary>
    /// 切换到下一个敌方聚焦目标；等待技能位置目标选择时不触发，避免与位置瞄准冲突。
    /// </summary>
    private void SwitchToNextEnemy() {
        if (unitManagerRef == null) {
            _logger.LogWarning("unitManagerRef is not assigned!");
            return;
        }
        if (playerInterfaceResRef?.IsWaitingSkillTarget == true)
            return;
        unitManagerRef.CycleEnemyTarget();
    }

    /// <summary>
    /// 更新鼠标悬停单位与地面瞄准点写入共享交互状态。
    /// MouseOnUnit 驱动轮廓高亮；MouseGroundPosition 供位置型技能瞄准使用。
    /// </summary>
    private void UpdateRaycast() {
        if (playerInterfaceResRef == null || cameraRef == null)
            return;

        var hit = RaycastUnitFromCamera();
        playerInterfaceResRef.MouseOnUnit = hit?.UnitShowRef;
        playerInterfaceResRef.MouseGroundPosition = RaycastGroundPosition();
    }

    /// <summary>从相机经鼠标位置发射线，命中单位交互层时返回对应的交互区域。</summary>
    private UnitShowArea3D? RaycastUnitFromCamera() {
        if (cameraRef == null)
            return null;

        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 from = cameraRef.ProjectRayOrigin(mousePos);
        Vector3 to = from + cameraRef.ProjectRayNormal(mousePos) * RaycastMaxDistance;

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, to, UnitCollisionLayer);
        query.CollideWithAreas = true;
        Dictionary result = cameraRef.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
            return null;
        return result["collider"].As<UnitShowArea3D>();
    }

    /// <summary>射线与地面平面（Y=0）的交点；无交点或朝下不交时返回 null。</summary>
    private Vector3? RaycastGroundPosition() {
        if (cameraRef == null)
            return null;

        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 origin = cameraRef.ProjectRayOrigin(mousePos);
        Vector3 dir = cameraRef.ProjectRayNormal(mousePos);

        if (Mathf.Abs(dir.Y) < 1e-6f)
            return null;
        float t = (GroundPlaneY - origin.Y) / dir.Y;
        if (t < 0f)
            return null;
        return origin + dir * t;
    }

    /// <summary>退出战斗时清零输入缓冲。</summary>
    public void Reset() {
        _moveDir = Vector2.Zero;
    }
}
