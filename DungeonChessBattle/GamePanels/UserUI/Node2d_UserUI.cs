using Godot;

namespace DungeonChessBattle;

public partial class Node2d_UserUI : Control {
    #region Exports

    [Export]
    UserInterfaceRes? userInterfaceRes;

    [Export]
    UnitsInScene_Show? unitsInSceneShowRef;

    [Export]
    SkillsList? skillsListRef;

    [Export]
    StateChangeInfo? stateChangeInfoRef;

    [Export]
    StateBarList? stateBarListRef;

    /// <summary>ViewModel 引用（由 UserOperationInterface_Info 在 _Ready 时注入）</summary>
    private UserOperationInterfaceInfo? _viewModel;

    #endregion

    bool isMouseOn = false;
    public bool IsMouseOn => isMouseOn;

    public override void _Ready() {
        MouseEntered += () => {
            isMouseOn = true;
        };
        MouseExited += () => {
            isMouseOn = false;
        };

        if (userInterfaceRes == null) {
            GD.PrintErr("[Node2d_UserUI] userInterfaceRes is not assigned!");
            return;
        }
        userInterfaceRes.FocusOnUnitChangedEvent += UpdateSkillList;
        if (userInterfaceRes.FocusOnUnit != null)
            UpdateSkillList(userInterfaceRes.FocusOnUnit);
    }

    #region ViewModel

    /// <summary>由 UserOperationInterface_Info 调用，注入 ViewModel 并订阅事件</summary>
    public void SetViewModel(UserOperationInterfaceInfo vm) {
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

    private void OnBattleBound() {
        // View 层绑定：将数据源注入到子组件
        if (unitsInSceneShowRef != null) {
            stateChangeInfoRef?.BindUnitsInScene(unitsInSceneShowRef.UnitsInSceneRes);
            stateBarListRef?.BindUnitsInScene(unitsInSceneShowRef.UnitsInSceneRes);
        }

        if (skillsListRef != null && unitsInSceneShowRef != null) {
            skillsListRef.UnitsInGameRef = unitsInSceneShowRef;
            skillsListRef.UserInterfaceRes = userInterfaceRes;
            skillsListRef.ViewModel = _viewModel;
        }
    }

    private void OnBattleUnbound() {
        // 清理 View 绑定
        skillsListRef?.ViewModel = null;
    }

    #endregion

    #region Public API

    public void UpdateSkillList(UnitGameShow unitShow) {
        skillsListRef?.UpdateSkillsList(unitShow);
    }

    /// <summary>当前是否在等待技能目标选择（由 SkillsList 驱动）</summary>
    public bool IsWaitSkillTarget() {
        return skillsListRef != null && skillsListRef.IsWaitTarget();
    }

    /// <summary>当前是否在等待移动目标选择（委托给 ViewModel）</summary>
    public bool IsWaitMoveTarget() {
        return _viewModel?.IsWaitingMoveTarget ?? false;
    }

    #endregion
}
