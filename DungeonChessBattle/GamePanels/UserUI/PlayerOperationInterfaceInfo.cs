using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 3D 交互桥接 View + ViewModel 层入口（MVVM 模式）。
/// 连接 Camera3D、PlayerUIRoot、EffectHints，
/// 封装 UI 交互状态和命令，作为 MainScene 与 UI 系统之间的唯一触点。
/// </summary>
public partial class PlayerOperationInterfaceInfo : Node {
    #region Exports

    /// <summary>场景 3D 相机引用。</summary>
    [Export]
    private Camera3D? camera3DRef;

    /// <summary>玩家 UI 根容器（View 层）引用。</summary>
    [Export]
    private PlayerUIRoot? playerUiRef;

    /// <summary>玩家界面资源引用。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>战斗单位管理器引用，用于发起本地玩家聚焦目标 RPC。</summary>
    [Export]
    private BattleUnitManager? battleUnitManagerRef;

    /// <summary>技能效果提示引用。</summary>
    [ExportGroup("Intrinsic Parameter")]
    [Export]
    private EffectHints? effectHintsRef;

    #endregion

    #region Bindable Properties (ViewModel)

    /// <summary>当前是否在等待技能目标选择。</summary>
    public bool IsWaitingSkillTarget {
        get; private set;
    }

    /// <summary>当前是否在等待移动目标选择。</summary>
    public bool IsWaitingMoveTarget {
        get; private set;
    }

    /// <summary>当前是否在等待任何目标选择。</summary>
    public bool IsWaitingTarget => IsWaitingSkillTarget || IsWaitingMoveTarget;

    /// <summary>战斗输入是否被 UI 阻塞（等待目标选择中）。</summary>
    public bool IsBlockingInput => IsWaitingTarget;

    #endregion

    #region Signals

    /// <summary>战斗绑定完成信号（供 View 层 PlayerUIRoot 订阅）。</summary>
    [Signal]
    public delegate void BattleBoundEventHandler();

    /// <summary>战斗解绑信号。</summary>
    [Signal]
    public delegate void BattleUnboundEventHandler();

    /// <summary>等待目标状态变化信号。</summary>
    [Signal]
    public delegate void WaitingTargetChangedEventHandler(bool isWaiting);

    #endregion

    #region Public API (MainScene 消费)

    /// <summary>进入战斗：触发 UI 绑定初始化。</summary>
    public void BindToBattle() {
        EmitSignal(SignalName.BattleBound);
    }

    /// <summary>退出战斗：清理 UI 绑定状态。</summary>
    public void UnbindFromBattle() {
        IsWaitingSkillTarget = false;
        IsWaitingMoveTarget = false;
        EmitSignal(SignalName.BattleUnbound);
    }

    #endregion

    #region Commands (ViewModel)

    /// <summary>View 层通知 VM 正在等待技能目标选择。</summary>
    /// <param name="waiting">是否进入等待技能目标状态。</param>
    public void NotifyWaitingSkillTarget(bool waiting) {
        var wasWaiting = IsWaitingTarget;
        IsWaitingSkillTarget = waiting;
        if (wasWaiting != IsWaitingTarget)
            EmitSignal(SignalName.WaitingTargetChanged, IsWaitingTarget);
    }

    /// <summary>View 层通知 VM 正在等待移动目标选择。</summary>
    /// <param name="waiting">是否进入等待移动目标状态。</param>
    public void NotifyWaitingMoveTarget(bool waiting) {
        var wasWaiting = IsWaitingTarget;
        IsWaitingMoveTarget = waiting;
        if (wasWaiting != IsWaitingTarget)
            EmitSignal(SignalName.WaitingTargetChanged, IsWaitingTarget);
    }

    /// <summary>取消所有等待状态。</summary>
    public void CancelAllWaiting() {
        var wasWaiting = IsWaitingTarget;
        IsWaitingSkillTarget = false;
        IsWaitingMoveTarget = false;
        if (wasWaiting)
            EmitSignal(SignalName.WaitingTargetChanged, false);
    }

    #endregion

    #region Mouse Interaction

    /// <summary>单位选择射线最大距离。</summary>
    private const float RaycastMaxDistance = 200f;

    /// <summary>单位交互碰撞层（对应 UnitShowArea3D 的 collision_layer=2048）。</summary>
    private const uint UnitCollisionLayer = 2048;

    /// <summary>地面平面 Y 坐标（场景地面高度）。</summary>
    private const float GroundPlaneY = 0f;

    /// <summary>
    /// 鼠标左键点击：请求设置或清除本地玩家单位的聚焦目标。
    /// 经 RPC 提交服务端，校验后写回 FocusTargetNetId 同步到所有客户端，
    /// 由 BattleUnitManager 桥接更新 FocusOnUnit 触发 FocusOnUnitChanged 信号。
    /// </summary>
    public override void _UnhandledInput(InputEvent @event) {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
            return;
        if (playerInterfaceRes == null || battleUnitManagerRef == null)
            return;

        var hit = RaycastUnitFromCamera();
        var targetNetId = hit?.UnitShowRef.Pawn.Id ?? 0;
        battleUnitManagerRef.SetLocalFocusTarget(targetNetId);
    }

    /// <summary>
    /// 每帧更新鼠标悬停单位与地面瞄准点。
    /// MouseOnUnit 驱动 FocusOnOutline 轮廓高亮；
    /// MouseGoundPosition 供位置型技能瞄准使用。
    /// </summary>
    public override void _Process(double delta) {
        if (playerInterfaceRes == null || camera3DRef == null)
            return;

        var hit = RaycastUnitFromCamera();
        playerInterfaceRes.MouseOnUnit = hit?.UnitShowRef;
        playerInterfaceRes.MouseGoundPosition = RaycastGroundPosition();
    }

    /// <summary>从相机经鼠标位置发射线，命中单位交互层时返回对应的交互区域。</summary>
    private UnitShowArea3D? RaycastUnitFromCamera() {
        if (camera3DRef == null)
            return null;

        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 from = camera3DRef.ProjectRayOrigin(mousePos);
        Vector3 to = from + camera3DRef.ProjectRayNormal(mousePos) * RaycastMaxDistance;

        var query = PhysicsRayQueryParameters3D.Create(from, to, UnitCollisionLayer);
        var result = camera3DRef.GetWorld3D().DirectSpaceState.IntersectRay(query);
        if (result.Count == 0)
            return null;
        return result["collider"].As<UnitShowArea3D>();
    }

    /// <summary>射线与地面平面（Y=0）的交点；无交点或朝下不交时返回 null。</summary>
    private Vector3? RaycastGroundPosition() {
        if (camera3DRef == null)
            return null;

        Vector2 mousePos = GetViewport().GetMousePosition();
        Vector3 origin = camera3DRef.ProjectRayOrigin(mousePos);
        Vector3 dir = camera3DRef.ProjectRayNormal(mousePos);

        if (Mathf.Abs(dir.Y) < 1e-6f)
            return null;
        float t = (GroundPlaneY - origin.Y) / dir.Y;
        if (t < 0f)
            return null;
        return origin + dir * t;
    }

    #endregion

    #region Godot Lifecycle

    /// <summary>
    /// 节点就绪：将 ViewModel 注入到 PlayerUIRoot（View 根容器）。
    /// </summary>
    public override void _Ready() {
        // 将 VM（self）注入到 PlayerUIRoot（View 根容器）
        playerUiRef?.SetViewModel(this);
    }

    #endregion
}
