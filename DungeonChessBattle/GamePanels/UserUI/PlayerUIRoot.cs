using Godot;

namespace DungeonChessBattle;

/// <summary>
/// 玩家 UI 根容器（View 层入口）。
/// 负责管理鼠标悬停状态、转发单位焦点变化到技能列表，
/// 并接收 ViewModel 注入以响应战斗绑定/解绑事件。
/// </summary>
public partial class PlayerUIRoot : Control {
    #region Exports

    /// <summary>玩家界面资源引用。</summary>
    [Export]
    private PlayerInterfaceRes? playerInterfaceRes;

    /// <summary>战斗单位管理器引用（持有场景单位集合资源）。</summary>
    [Export]
    private BattleUnitManager? unitsInSceneShowRef;

    /// <summary>技能列表面板引用。</summary>
    [Export]
    private SkillsList? skillsListRef;

    /// <summary>状态变化信息面板引用。</summary>
    [Export]
    private StateChangeInfo? stateChangeInfoRef;

    /// <summary>状态条列表面板引用。</summary>
    [Export]
    private StateBarList? stateBarListRef;

    /// <summary>ViewModel 引用（由 PlayerOperationInterfaceInfo 在 _Ready 时注入）。</summary>
    private PlayerOperationInterfaceInfo? _viewModel;

    #endregion

    /// <summary>鼠标是否悬停在 UI 上。</summary>
    public bool IsMouseOn { get; private set; } = false;

    /// <summary>
    /// 节点就绪：监听鼠标悬停状态并订阅焦点单位变化事件。
    /// </summary>
    public override void _Ready() {
        MouseEntered += () => {
            IsMouseOn = true;
        };
        MouseExited += () => {
            IsMouseOn = false;
        };

        if (playerInterfaceRes == null) {
            GD.PrintErr("[PlayerUIRoot] playerInterfaceRes is not assigned!");
            return;
        }
        playerInterfaceRes.FocusOnUnitChanged += UpdateSkillList;
        if (playerInterfaceRes.FocusOnUnit != null)
            UpdateSkillList(playerInterfaceRes.FocusOnUnit);
    }

    #region ViewModel

    /// <summary>由 PlayerOperationInterfaceInfo 调用，注入 ViewModel 并订阅事件。</summary>
    public void SetViewModel(PlayerOperationInterfaceInfo vm) {
        if (_viewModel != null) {
            _viewModel.BattleBound -= OnBattleBound;
            _viewModel.BattleUnbound -= OnBattleUnbound;
        }

        _viewModel = vm;
        _viewModel.BattleBound += OnBattleBound;
        _viewModel.BattleUnbound += OnBattleUnbound;
    }

    #endregion

    #region ViewModel Signal Handlers

    /// <summary>
    /// 战斗绑定：将单位集合数据源注入到子组件并初始化技能列表。
    /// </summary>
    private void OnBattleBound() {
        // View 层绑定：将数据源注入到子组件
        if (unitsInSceneShowRef != null) {
            stateChangeInfoRef?.BindUnitsInScene(unitsInSceneShowRef.UnitsInSceneRes);
            unitsInSceneShowRef.LocalUnitShowReady += OnLocalUnitShowReady;
        }

        if (skillsListRef != null && unitsInSceneShowRef != null) {
            skillsListRef.UnitsInGameRef = unitsInSceneShowRef;
            skillsListRef.PlayerInterfaceRes = playerInterfaceRes;
            skillsListRef.ViewModel = _viewModel;
        }

        TryShowLocalUnit();
    }

    /// <summary>
    /// 战斗解绑：清理子组件中的 ViewModel 引用并退订本地单位事件。
    /// </summary>
    private void OnBattleUnbound() {
        // 清理 View 绑定
        if (unitsInSceneShowRef != null)
            unitsInSceneShowRef.LocalUnitShowReady -= OnLocalUnitShowReady;
        skillsListRef?.ViewModel = null;
    }

    /// <summary>
    /// 本地玩家单位视图就绪：绑定动态友方阵营状态列表并刷新自身技能列表。
    /// </summary>
    /// <param name="localShow">本地玩家控制的单位视图。</param>
    private void OnLocalUnitShowReady(UnitGameShow localShow) {
        if (unitsInSceneShowRef != null)
            stateBarListRef?.BindUnitsInScene(unitsInSceneShowRef.UnitsInSceneRes, localShow.Pawn.Camp.Value);
        UpdateSkillList(localShow);
    }

    /// <summary>
    /// 尝试立即展示本地单位：本地单位可能已随单位管理器 Bind 同步生成。
    /// </summary>
    private void TryShowLocalUnit() {
        var localShow = unitsInSceneShowRef?.LocalUnitShow;
        if (localShow != null)
            OnLocalUnitShowReady(localShow);
    }

    #endregion

    #region Public API

    /// <summary>
    /// 焦点单位变化回调，转发到技能列表刷新。
    /// </summary>
    /// <param name="unitShow">新的焦点单位。</param>
    public void UpdateSkillList(UnitGameShow unitShow) {
        skillsListRef?.UpdateSkillsList(unitShow);
    }

    /// <summary>当前是否在等待技能目标选择（由 SkillsList 驱动）。</summary>
    public bool IsWaitSkillTarget() {
        return skillsListRef != null && skillsListRef.IsWaitTarget();
    }

    /// <summary>当前是否在等待移动目标选择（委托给 ViewModel）。</summary>
    public bool IsWaitMoveTarget() {
        return _viewModel?.IsWaitingMoveTarget ?? false;
    }

    #endregion
}
