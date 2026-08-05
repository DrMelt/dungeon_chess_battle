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

    /// <summary>技能效果提示引用。</summary>
    [ExportGroup("Intrinsic Parameter")]
    [Export]
    private EffectHints? effectHintsRef;

    #endregion

    #region ViewModel State

    /// <summary>场景单位集合引用</summary>
    public UnitsInSceneView? UnitsInScene {
        get; set;
    }

    #endregion

    #region Bindable Properties (ViewModel)

    /// <summary>当前是否在等待技能目标选择</summary>
    public bool IsWaitingSkillTarget {
        get; private set;
    }

    /// <summary>当前是否在等待移动目标选择</summary>
    public bool IsWaitingMoveTarget {
        get; private set;
    }

    /// <summary>当前是否在等待任何目标选择</summary>
    public bool IsWaitingTarget => IsWaitingSkillTarget || IsWaitingMoveTarget;

    /// <summary>战斗输入是否被 UI 阻塞（等待目标选择中）</summary>
    public bool IsBlockingInput => IsWaitingTarget;

    #endregion

    #region Signals

    /// <summary>战斗绑定完成信号（供 View 层 PlayerUIRoot 订阅）</summary>
    [Signal]
    public delegate void BattleBoundEventHandler();

    /// <summary>战斗解绑信号</summary>
    [Signal]
    public delegate void BattleUnboundEventHandler();

    /// <summary>等待目标状态变化信号</summary>
    [Signal]
    public delegate void WaitingTargetChangedEventHandler(bool isWaiting);

    #endregion

    #region Public API (MainScene 消费)

    /// <summary>进入战斗：触发 UI 绑定初始化</summary>
    public void BindToBattle() {
        EmitSignal(SignalName.BattleBound);
    }

    /// <summary>退出战斗：清理 UI 绑定状态</summary>
    public void UnbindFromBattle() {
        IsWaitingSkillTarget = false;
        IsWaitingMoveTarget = false;
        EmitSignal(SignalName.BattleUnbound);
    }

    #endregion

    #region Commands (ViewModel)

    /// <summary>View 层通知 VM 正在等待技能目标选择</summary>
    /// <param name="waiting">是否进入等待技能目标状态。</param>
    public void NotifyWaitingSkillTarget(bool waiting) {
        var wasWaiting = IsWaitingTarget;
        IsWaitingSkillTarget = waiting;
        if (wasWaiting != IsWaitingTarget)
            EmitSignal(SignalName.WaitingTargetChanged, IsWaitingTarget);
    }

    /// <summary>View 层通知 VM 正在等待移动目标选择</summary>
    /// <param name="waiting">是否进入等待移动目标状态。</param>
    public void NotifyWaitingMoveTarget(bool waiting) {
        var wasWaiting = IsWaitingTarget;
        IsWaitingMoveTarget = waiting;
        if (wasWaiting != IsWaitingTarget)
            EmitSignal(SignalName.WaitingTargetChanged, IsWaitingTarget);
    }

    /// <summary>取消所有等待状态</summary>
    public void CancelAllWaiting() {
        var wasWaiting = IsWaitingTarget;
        IsWaitingSkillTarget = false;
        IsWaitingMoveTarget = false;
        if (wasWaiting)
            EmitSignal(SignalName.WaitingTargetChanged, false);
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
