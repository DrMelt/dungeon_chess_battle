using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 3D 交互桥接 View + ViewModel 层入口（MVVM 模式）。
/// 连接 Camera3D、Node2d_UserUI、EffectHints，
/// 封装 UI 交互状态和命令，作为 MainScene 与 UI 系统之间的唯一触点。
/// </summary>
public partial class UserOperationInterfaceInfo : Node {
    #region Exports

    [Export]
    Camera3D? camera3DRef;

    [Export]
    Node2d_UserUI? node2dUiRef;

    [Export]
    UserInterfaceRes? userInterfaceRes;

    [ExportGroup("Intrinsic Parameter")]
    [Export]
    EffectHints? effectHintsRef;

    #endregion

    #region ViewModel State

    /// <summary>场景单位集合引用</summary>
    public UnitsInScene_Show? UnitsInScene {
        get; set;
    }

    private bool _isWaitingSkillTarget;
    private bool _isWaitingMoveTarget;

    #endregion

    #region Bindable Properties (ViewModel)

    /// <summary>当前是否在等待技能目标选择</summary>
    public bool IsWaitingSkillTarget => _isWaitingSkillTarget;

    /// <summary>当前是否在等待移动目标选择</summary>
    public bool IsWaitingMoveTarget => _isWaitingMoveTarget;

    /// <summary>当前是否在等待任何目标选择</summary>
    public bool IsWaitingTarget => _isWaitingSkillTarget || _isWaitingMoveTarget;

    /// <summary>战斗输入是否被 UI 阻塞（等待目标选择中）</summary>
    public bool IsBlockingInput => IsWaitingTarget;

    #endregion

    #region Signals

    /// <summary>战斗绑定完成信号（供 View 层 Node2d_UserUI 订阅）</summary>
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
        _isWaitingSkillTarget = false;
        _isWaitingMoveTarget = false;
        EmitSignal(SignalName.BattleUnbound);
    }

    #endregion

    #region Commands (ViewModel)

    /// <summary>View 层通知 VM 正在等待技能目标选择</summary>
    public void NotifyWaitingSkillTarget(bool waiting) {
        var wasWaiting = IsWaitingTarget;
        _isWaitingSkillTarget = waiting;
        if (wasWaiting != IsWaitingTarget)
            EmitSignal(SignalName.WaitingTargetChanged, IsWaitingTarget);
    }

    /// <summary>View 层通知 VM 正在等待移动目标选择</summary>
    public void NotifyWaitingMoveTarget(bool waiting) {
        var wasWaiting = IsWaitingTarget;
        _isWaitingMoveTarget = waiting;
        if (wasWaiting != IsWaitingTarget)
            EmitSignal(SignalName.WaitingTargetChanged, IsWaitingTarget);
    }

    /// <summary>取消所有等待状态</summary>
    public void CancelAllWaiting() {
        var wasWaiting = IsWaitingTarget;
        _isWaitingSkillTarget = false;
        _isWaitingMoveTarget = false;
        if (wasWaiting)
            EmitSignal(SignalName.WaitingTargetChanged, false);
    }

    #endregion

    #region Godot Lifecycle

    public override void _Ready() {
        // 将 VM（self）注入到 Node2d_UserUI（View 根容器）
        node2dUiRef?.SetViewModel(this);
    }

    #endregion
}
